using System.Text.Json;
using Ascend.Api.Data;
using Ascend.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected readonly JwtOptions Jwt;

    protected ApiControllerBase(IOptions<JwtOptions>? jwt = null)
    {
        Jwt = jwt?.Value ?? new JwtOptions();
    }

    protected IActionResult Err(string message, int status) => StatusCode(status, new { error = message });

    protected IActionResult Json(object? value) => new JsonResult(value);

    protected async Task<JsonElement?> ReadBodyAsync()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var text = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(text)) return null;
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    protected static string? GetStr(JsonElement? body, string name) =>
        body is JsonElement el ? GetStr(el, name) : null;

    protected static string? GetStr(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    protected static double? GetNum(JsonElement? body, string name)
    {
        if (body is JsonElement el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String &&
                double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    protected static bool GetBool(JsonElement? body, string name) =>
        body is JsonElement el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && AiClient.JsTruthy(v);

    protected static int JsRound(double d) => (int)Math.Round(d, MidpointRounding.AwayFromZero);

    protected static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    protected async Task<SessionUser?> CurrentUser()
    {
        // 1. Prefer JWT bearer: lets modern clients use Authorization: Bearer <jwt>.
        var auth = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var raw = auth.Substring("Bearer ".Length).Trim();
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            try
            {
                var principal = handler.ValidateToken(raw, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Jwt.Issuer,
                    ValidAudience = Jwt.Audience,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(Jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                }, out _);
                var fromJwt = JwtService.ReadUser(principal);
                if (fromJwt != null) return fromJwt;
            }
            catch { /* fall through to cookie */ }
        }

        // 2. Legacy cookie session (unchanged behaviour).
        return await SessionService.GetUserAsync(HttpContext);
    }

    protected static JsonElement? ToJsonElement(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
