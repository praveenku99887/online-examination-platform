using Ascend.Api.Data;
using Ascend.Api.Events;
using Ascend.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

[Route("api/ai")]
public class AiController : ApiControllerBase
{
    private readonly IAscendEventPublisher _events;

    public AiController(IOptions<JwtOptions> jwt, IAscendEventPublisher events)
        : base(jwt) => _events = events;

    [HttpGet("provider")]
    public async Task<IActionResult> GetProvider()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        await using var conn = await Db.OpenConnectionAsync();
        var p = await conn.QueryFirstOrDefaultAsync<AiProviderRow>(
            "SELECT * FROM ai_providers WHERE user_id = @uid", new { uid = user.Id.ToString() });
        if (p == null) return Json(new { configured = false });

        var masked = p.ApiKey.Length > 7
            ? p.ApiKey[..3] + "••••••" + p.ApiKey[^4..]
            : "••••••";
        return Json(new { configured = true, baseUrl = p.BaseUrl, model = p.Model, maskedKey = masked });
    }

    [HttpPost("provider")]
    public async Task<IActionResult> SaveProvider()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        var body = await ReadBodyAsync();
        var baseUrl = GetStr(body, "baseUrl")?.Trim();
        var apiKey = GetStr(body, "apiKey")?.Trim();
        var model = (GetStr(body, "model") ?? "").Trim();
        if (model.Length == 0) model = "gpt-4o-mini";
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
            return Err("Base URL and API key are required.", 400);

        var normalized = AiClient.NormalizeBaseUrl(baseUrl);

        await using var conn = await Db.OpenConnectionAsync();
        var existing = await conn.QueryFirstOrDefaultAsync<AiProviderRow>(
            "SELECT * FROM ai_providers WHERE user_id = @uid", new { uid = user.Id.ToString() });
        if (existing != null)
        {
            await conn.ExecuteAsync(
                "UPDATE ai_providers SET base_url = @baseUrl, api_key = @apiKey, model = @model, updated_at = @updatedAt WHERE id = @id",
                new { baseUrl = normalized, apiKey, model, updatedAt = Db.NowDb(), id = existing.Id.ToString() });
        }
        else
        {
            await conn.ExecuteAsync(
                "INSERT INTO ai_providers (id, user_id, base_url, api_key, model, created_at, updated_at) VALUES (@id, @uid, @baseUrl, @apiKey, @model, @now, @now)",
                new { id = Db.NewId(), uid = user.Id.ToString(), baseUrl = normalized, apiKey, model, now = Db.NowDb() });
        }
        return Json(new { ok = true });
    }

    [HttpDelete("provider")]
    public async Task<IActionResult> DeleteProvider()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        await using var conn = await Db.OpenConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM ai_providers WHERE user_id = @uid", new { uid = user.Id.ToString() });
        return Json(new { ok = true });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        var body = await ReadBodyAsync();
        var baseUrl = GetStr(body, "baseUrl");
        var apiKey = GetStr(body, "apiKey");
        if (string.IsNullOrEmpty(baseUrl?.Trim()) || string.IsNullOrEmpty(apiKey?.Trim()))
            return Err("Base URL and API key are required.", 400);

        var model = (GetStr(body, "model") ?? "").Trim();
        if (model.Length == 0) model = "gpt-4o-mini";

        var result = await AiClient.TestProviderAsync(new ProviderCfg(baseUrl!, apiKey!, model));
        return Json(result);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        await using var conn = await Db.OpenConnectionAsync();
        var p = await conn.QueryFirstOrDefaultAsync<AiProviderRow>(
            "SELECT * FROM ai_providers WHERE user_id = @uid", new { uid = user.Id.ToString() });
        if (p == null)
            return Err("No AI provider configured. Connect a provider first.", 400);

        var body = await ReadBodyAsync();
        var mode = GetStr(body, "mode") == "material" ? "material" : "topic";
        var count = Clamp(JsRound(GetNum(body, "count") ?? 8), 3, 20);
        var difficultyRaw = GetStr(body, "difficulty") ?? "";
        var difficulty = difficultyRaw is "foundational" or "intermediate" or "advanced" ? difficultyRaw : "intermediate";
        var minutes = Clamp(JsRound(GetNum(body, "durationMinutes") ?? count * 1.5), 5, 180);

        string input;
        if (mode == "topic")
        {
            input = (GetStr(body, "topic") ?? "").Trim();
            if (input.Length < 3)
                return Err("Give the AI a topic to research.", 400);
        }
        else
        {
            var material = (GetStr(body, "material") ?? "").Trim();
            input = material.Length > 16000 ? material[..16000] : material;
            if (input.Length < 200)
                return Err("Provide at least 200 characters of source material.", 400);
        }

        try
        {
            var raw = await AiClient.ChatCompletionAsync(
                new ProviderCfg(p.BaseUrl, p.ApiKey, p.Model),
                new object[]
                {
                    new Dictionary<string, string> { ["role"] = "system", ["content"] = AiClient.SystemPrompt },
                    new Dictionary<string, string> { ["role"] = "user", ["content"] = AiClient.BuildPrompt(mode, input, count, difficulty) },
                },
                temperature: 0.55,
                maxTokens: 8192,
                timeoutMs: 150_000);

            var parsed = AiClient.ExtractJsonElement(raw);
            var draft = AiClient.NormalizeDraft(
                parsed,
                count,
                minutes,
                mode == "topic" ? input[..Math.Min(60, input.Length)] : "Uploaded Material");
            if (draft.Questions.Count == 0)
                return Err("The AI returned no usable questions. Try again.", 502);

            _events.Publish(AscendEventTypes.AiDraftGenerated, user.Id.ToString(), new { userId = user.Id, mode, count = draft.Questions.Count, difficulty });
            return Json(new { ok = true, draft });
        }
        catch (Exception err)
        {
            Console.Error.WriteLine($"[ai:generate] {err}");
            return Err(err.Message, 502);
        }
    }
}
