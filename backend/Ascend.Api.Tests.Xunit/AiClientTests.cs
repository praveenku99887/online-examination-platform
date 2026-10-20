using System.Text.Json;
using Ascend.Api.Services;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

public sealed class AiClientTests
{
    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Theory]
    [InlineData("api.example.com/v1///", "https://api.example.com/v1")]
    [InlineData("http://localhost:1234/", "http://localhost:1234")]
    [InlineData("  https://api.openai.com/v1  ", "https://api.openai.com/v1")]
    public void NormalizeBaseUrl_adds_scheme_and_removes_trailing_slashes(
        string input, string expected)
    {
        Assert.Equal(expected, AiClient.NormalizeBaseUrl(input));
    }

    [Fact]
    public void ExtractJsonElement_handles_markdown_fences()
    {
        var json = AiClient.ExtractJsonElement(
            "```json\n{\"title\":\"Fenced\",\"questions\":[]}\n```");

        Assert.Equal("Fenced", json.GetProperty("title").GetString());
    }

    [Fact]
    public void ExtractJsonElement_handles_surrounding_prose()
    {
        var json = AiClient.ExtractJsonElement(
            "Here is the assessment:\n{\"ok\":true}\nThanks!");

        Assert.True(json.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void ExtractJsonElement_throws_when_no_object_exists()
    {
        var error = Assert.Throws<Exception>(
            () => AiClient.ExtractJsonElement("there is no json here"));

        Assert.Contains("did not contain JSON", error.Message);
    }

    [Fact]
    public void NormalizeDraft_drops_malformed_questions_and_keeps_one_correct_option()
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

        var draft = AiClient.NormalizeDraft(raw, count: 5, minutes: 12, fallbackSubject: "Fallback");

        Assert.Equal("AI Draft", draft.Title);
        Assert.Equal("Biology", draft.Subject);
        Assert.Equal(12, draft.DurationMinutes);
        Assert.Single(draft.Questions);

        var question = draft.Questions[0];
        Assert.Equal("General", question.Topic);
        Assert.Equal("because", question.Explanation);
        Assert.Equal(2, question.Options.Count);
        Assert.Single(question.Options, o => o.Correct);
        Assert.Equal("A", question.Options[0].Text);
        Assert.False(question.Options[1].Correct);
    }

    [Fact]
    public void NormalizeDraft_uses_fallbacks_and_pads_options()
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

        var draft = AiClient.NormalizeDraft(raw, count: 3, minutes: 8, fallbackSubject: "Fallback");

        Assert.Equal("AI Generated Assessment", draft.Title);
        Assert.Equal("Fallback", draft.Subject);
        Assert.Equal(8, draft.DurationMinutes);
        Assert.Single(draft.Questions);
        Assert.Equal(2, draft.Questions[0].Options.Count);
        Assert.True(draft.Questions[0].Options[0].Correct);
        Assert.False(draft.Questions[0].Options[1].Correct);
    }

    [Fact]
    public void BuildPrompt_for_topic_contains_count_difficulty_and_rules()
    {
        var prompt = AiClient.BuildPrompt("topic", "Cell Biology", 4, "advanced");

        Assert.Contains("Cell Biology", prompt);
        Assert.Contains("exactly 4 questions", prompt);
        Assert.Contains("advanced", prompt);
        Assert.Contains("Exactly 4 options per question", prompt);
    }

    [Fact]
    public void BuildPrompt_for_material_uses_source_as_only_knowledge_base()
    {
        const string material = "The mitochondria is the powerhouse of the cell.";
        var prompt = AiClient.BuildPrompt("material", material, 3, "foundational");

        Assert.Contains("ONLY knowledge base", prompt);
        Assert.Contains(material, prompt);
        Assert.Contains("exactly 3 questions", prompt);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("\"\"", false)]
    [InlineData("\"x\"", true)]
    [InlineData("[]", true)]
    public void JsTruthy_matches_javascript_semantics(string json, bool expected)
    {
        Assert.Equal(expected, AiClient.JsTruthy(Json(json)));
    }
}
