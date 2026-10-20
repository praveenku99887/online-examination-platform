using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ascend.Api.Data;
using Ascend.Api.Services;
using Ascend.Api.Tests.Shared;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace Ascend.Api.Tests.Nunit;

[TestFixture]
public sealed class AiAndSecurityTests
{
    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [TestCase("api.example.com/v1///", "https://api.example.com/v1")]
    [TestCase("http://localhost:1234/", "http://localhost:1234")]
    [TestCase("  https://api.openai.com/v1  ", "https://api.openai.com/v1")]
    public void NormalizeBaseUrl_adds_scheme_and_trims_slashes(string input, string expected)
    {
        Assert.That(AiClient.NormalizeBaseUrl(input), Is.EqualTo(expected));
    }

    [Test]
    public void ExtractJsonElement_handles_markdown_and_surrounding_text()
    {
        var fenced = AiClient.ExtractJsonElement(
            "```json\n{\"title\":\"Fenced\"}\n```");
        var prose = AiClient.ExtractJsonElement(
            "Result:\n{\"ok\":true}\nDone");

        Assert.That(fenced.GetProperty("title").GetString(), Is.EqualTo("Fenced"));
        Assert.That(prose.GetProperty("ok").GetBoolean(), Is.True);
    }

    [Test]
    public void ExtractJsonElement_throws_for_non_json()
    {
        Assert.That(
            () => AiClient.ExtractJsonElement("no json here"),
            Throws.TypeOf<Exception>().With.Message.Contains("did not contain JSON"));
    }

    [Test]
    public void NormalizeDraft_cleans_malformed_questions_and_duplicate_correct_answers()
    {
        var raw = Json("""
        {
          "title": "AI Draft",
          "description": "desc",
          "subject": "Biology",
          "questions": [
            {
              "text": "Q1",
              "topic": "",
              "explanation": "because",
              "options": [
                { "text": "A", "correct": true },
                { "text": "B", "correct": true },
                { "text": "", "correct": false }
              ]
            },
            { "options": [ { "text": "C", "correct": true } ] },
            "not-an-object"
          ]
        }
        """);

        var draft = AiClient.NormalizeDraft(raw, 5, 12, "Fallback");

        Assert.That(draft.Title, Is.EqualTo("AI Draft"));
        Assert.That(draft.Subject, Is.EqualTo("Biology"));
        Assert.That(draft.DurationMinutes, Is.EqualTo(12));
        Assert.That(draft.Questions, Has.Count.EqualTo(1));
        Assert.That(draft.Questions[0].Topic, Is.EqualTo("General"));
        Assert.That(draft.Questions[0].Options, Has.Count.EqualTo(2));
        Assert.That(draft.Questions[0].Options.Count(o => o.Correct), Is.EqualTo(1));
    }

    [Test]
    public void NormalizeDraft_uses_fallbacks_and_adds_a_second_option()
    {
        var raw = Json("""
        {
          "questions": [
            {
              "text": "Only question",
              "options": [ { "text": "Only option", "correct": false } ]
            }
          ]
        }
        """);

        var draft = AiClient.NormalizeDraft(raw, 3, 8, "Fallback");

        Assert.That(draft.Title, Is.EqualTo("AI Generated Assessment"));
        Assert.That(draft.Subject, Is.EqualTo("Fallback"));
        Assert.That(draft.Questions[0].Options, Has.Count.EqualTo(2));
        Assert.That(draft.Questions[0].Options[0].Correct, Is.True);
    }

    [Test]
    public void BuildPrompt_contains_topic_count_and_difficulty()
    {
        var prompt = AiClient.BuildPrompt("topic", "Cell Biology", 4, "advanced");

        Assert.That(prompt, Does.Contain("Cell Biology"));
        Assert.That(prompt, Does.Contain("exactly 4 questions"));
        Assert.That(prompt, Does.Contain("advanced"));
        Assert.That(prompt, Does.Contain("Exactly 4 options per question"));
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    [TestCase("null", false)]
    [TestCase("0", false)]
    [TestCase("1", true)]
    [TestCase("\"\"", false)]
    [TestCase("\"x\"", true)]
    [TestCase("[]", true)]
    public void JsTruthy_matches_javascript_semantics(string json, bool expected)
    {
        Assert.That(AiClient.JsTruthy(Json(json)), Is.EqualTo(expected));
    }

    [Test]
    public void Password_hash_and_verify_round_trip()
    {
        var hash = Passwords.Hash("secret123");

        Assert.That(hash, Is.Not.EqualTo("secret123"));
        Assert.That(Passwords.Verify("secret123", hash), Is.True);
        Assert.That(Passwords.Verify("wrong", hash), Is.False);
        Assert.That(Passwords.Verify("secret123", "not-a-hash"), Is.False);
    }

    [Test]
    public void Jwt_round_trips_user_identity()
    {
        var options = new JwtOptions
        {
            Key = AscendWebApplicationFactory.TestJwtKey,
            Issuer = "Ascend",
            Audience = "Ascend",
            ExpiryMinutes = 60,
        };
        var user = new SessionUser(
            Guid.NewGuid(), "Test Student", "student@example.com", "student");

        var token = JwtService.CreateToken(options, user);
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = options.Issuer,
                ValidAudience = options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(options.Key)),
                ClockSkew = TimeSpan.Zero,
            },
            out _);

        var parsed = JwtService.ReadUser(principal);

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.Id, Is.EqualTo(user.Id));
        Assert.That(parsed.Role, Is.EqualTo("student"));
    }

    [Test]
    public void Jwt_read_user_rejects_unknown_roles()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "admin"),
        }, "test"));

        Assert.That(JwtService.ReadUser(principal), Is.Null);
    }
}
