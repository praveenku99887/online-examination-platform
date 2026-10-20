using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ascend.Api.Data;

namespace Ascend.Api.Services;

public static class AiClient
{
    // Infinite transport timeout — per-call budgets come from the linked CTS below.
    private static readonly HttpClient Http = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public const string SystemPrompt =
        "You are Ascend's senior assessment architect — a former university examiner and instructional designer. " +
        "You design rigorous, unambiguous multiple-choice assessments. Every distractor is a *diagnostic tool*: " +
        "it targets a specific misconception and its \"whyWrong\" explains that misconception precisely. " +
        "\"explanation\" teaches why the correct option is correct with real depth. " +
        "Output STRICT JSON only — no markdown fences, no commentary.";

    public static string NormalizeBaseUrl(string raw)
    {
        var baseUrl = Regex.Replace(raw.Trim(), "/+$", "");
        if (!Regex.IsMatch(baseUrl, "^https?://", RegexOptions.IgnoreCase)) baseUrl = "https://" + baseUrl;
        return baseUrl;
    }

    public static async Task<string> ChatCompletionAsync(
        ProviderCfg cfg,
        IEnumerable<object> messages,
        double temperature = 0.4,
        int maxTokens = 8192,
        int timeoutMs = 120_000,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeBaseUrl(cfg.BaseUrl);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = cfg.Model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = false,
        };

        // Gemini and other OpenAI-compatible providers return transient 5xx
        // (503 "high demand") under load. Retry with a short backoff instead of
        // failing the whole generation on the first blip.
        var backoffs = new[] { 0, 2_000, 5_000 };
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < backoffs.Length; attempt++)
        {
            if (attempt > 0)
            {
                try { await Task.Delay(backoffs[attempt], cts.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new Exception("This operation was aborted");
                }
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/chat/completions");
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.ApiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage res;
            string text;
            try
            {
                res = await Http.SendAsync(req, cts.Token);
                text = await res.Content.ReadAsStringAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new Exception("This operation was aborted");
            }
            catch (HttpRequestException err)
            {
                throw new Exception(err.Message);
            }

            using (res)
            {
                if (!res.IsSuccessStatusCode)
                {
                    var snippet = text.Length > 300 ? text[..300] : text;
                    var reason = snippet.Length > 0 ? snippet : (res.ReasonPhrase ?? "");
                    lastFailure = new Exception($"Provider responded {(int)res.StatusCode}: {reason}");
                    // Retry server-side hiccups; surface client errors immediately.
                    var status = (int)res.StatusCode;
                    if (status is 500 or 502 or 503 or 504 && attempt < backoffs.Length - 1)
                        continue;
                    throw lastFailure;
                }

                string? content = null;
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("choices", out var choices) &&
                        choices.ValueKind == JsonValueKind.Array &&
                        choices.GetArrayLength() > 0 &&
                        choices[0].ValueKind == JsonValueKind.Object &&
                        choices[0].TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.Object &&
                        message.TryGetProperty("content", out var contentEl) &&
                        contentEl.ValueKind == JsonValueKind.String)
                    {
                        content = contentEl.GetString();
                    }
                }
                catch (JsonException)
                {
                }

                if (string.IsNullOrEmpty(content))
                    throw new Exception("Provider returned an empty completion.");
                return content;
            }
        }

        throw lastFailure ?? new Exception("Provider did not respond.");
    }

    public static async Task<object> TestProviderAsync(ProviderCfg cfg)
    {
        var started = Environment.TickCount64;
        try
        {
            var reply = await ChatCompletionAsync(
                cfg,
                new object[] { new Dictionary<string, string> { ["role"] = "user", ["content"] = "Reply with exactly: ok" } },
                maxTokens: 8,
                timeoutMs: 25_000);
            var sliced = reply.Length > 40 ? reply[..40] : reply;
            return new
            {
                ok = true,
                latencyMs = Environment.TickCount64 - started,
                detail = $"Connected. Model replied \"{sliced.Trim()}\"",
            };
        }
        catch (Exception err)
        {
            try
            {
                var baseUrl = NormalizeBaseUrl(cfg.BaseUrl);
                using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/models");
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.ApiKey);
                using var cts = new CancellationTokenSource(15_000);
                using var res = await Http.SendAsync(req, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    return new
                    {
                        ok = true,
                        latencyMs = Environment.TickCount64 - started,
                        detail = "Connected via /models (chat endpoint unreachable).",
                    };
                }
            }
            catch
            {
                // ignore
            }
            return new { ok = false, latencyMs = Environment.TickCount64 - started, detail = err.Message };
        }
    }

    public static JsonElement ExtractJsonElement(string raw)
    {
        var text = raw.Trim();
        text = Regex.Replace(text, "^```(?:json)?", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "```$", "");
        text = text.Trim();
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first == -1 || last == -1 || last <= first)
            throw new Exception("AI response did not contain JSON.");
        var candidate = text.Substring(first, last - first + 1);
        try
        {
            return ParseClone(candidate);
        }
        catch (JsonException)
        {
            var repaired = candidate;
            var opens = repaired.Count(c => c is '{' or '[') - repaired.Count(c => c is '}' or ']');
            var inString = repaired.Count(c => c == '"') % 2 == 1;
            if (inString) repaired += "\"";
            for (var i = 0; i < opens; i++)
            {
                var lastOpen = Math.Max(repaired.LastIndexOf('{'), repaired.LastIndexOf('['));
                repaired += repaired[lastOpen] == '{' ? "}" : "]";
            }
            return ParseClone(repaired);
        }
    }

    private static JsonElement ParseClone(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static bool JsTruthy(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
        JsonValueKind.True => true,
        JsonValueKind.Number => e.GetDouble() != 0,
        JsonValueKind.String => (e.GetString() ?? "").Length > 0,
        _ => true,
    };

    public static DraftAssessment NormalizeDraft(JsonElement raw, int count, int minutes, string fallbackSubject)
    {
        var questions = new List<DraftQuestion>();

        if (raw.ValueKind == JsonValueKind.Object &&
            raw.TryGetProperty("questions", out var qsEl) &&
            qsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var q in qsEl.EnumerateArray())
            {
                if (questions.Count >= Math.Max(count, 1)) break;
                if (q.ValueKind != JsonValueKind.Object) continue;
                if (!q.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String) continue;
                if (!q.TryGetProperty("options", out var optsEl) || optsEl.ValueKind != JsonValueKind.Array) continue;

                var opts = new List<DraftOption>();
                foreach (var o in optsEl.EnumerateArray())
                {
                    if (opts.Count >= 6) break;
                    if (o.ValueKind != JsonValueKind.Object) continue;
                    if (!o.TryGetProperty("text", out var oTextEl) || oTextEl.ValueKind != JsonValueKind.String) continue;
                    var oText = oTextEl.GetString() ?? "";
                    if (oText.Trim().Length == 0) continue;
                    var correct = o.TryGetProperty("correct", out var cEl) && JsTruthy(cEl);
                    string whyWrong;
                    if (correct)
                    {
                        whyWrong = "";
                    }
                    else
                    {
                        var rawWhy = o.TryGetProperty("whyWrong", out var wEl) && wEl.ValueKind == JsonValueKind.String
                            ? wEl.GetString()
                            : null;
                        whyWrong = (rawWhy ?? "This option contradicts the concept being tested.").Trim();
                    }
                    opts.Add(new DraftOption { Text = oText.Trim(), Correct = correct, WhyWrong = whyWrong });
                }

                while (opts.Count < 2)
                    opts.Add(new DraftOption { Text = "None of the above", Correct = false, WhyWrong = "One of the stated options is correct." });

                if (!opts.Any(o => o.Correct)) opts[0].Correct = true;

                var seenCorrect = false;
                foreach (var o in opts)
                {
                    if (o.Correct && seenCorrect)
                    {
                        o.Correct = false;
                    }
                    else if (o.Correct)
                    {
                        seenCorrect = true;
                    }
                }

                var topicRaw = q.TryGetProperty("topic", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : null;
                var explanationRaw = q.TryGetProperty("explanation", out var eEl) && eEl.ValueKind == JsonValueKind.String ? eEl.GetString() : null;

                questions.Add(new DraftQuestion
                {
                    Text = textEl.GetString()!.Trim(),
                    Topic = string.IsNullOrEmpty(topicRaw) ? "General" : topicRaw!.Trim(),
                    Explanation = (explanationRaw ?? "").Trim(),
                    Options = opts,
                });
            }
        }

        var titleRaw = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String
            ? titleEl.GetString()
            : null;
        var descRaw = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("description", out var dEl) && dEl.ValueKind == JsonValueKind.String
            ? dEl.GetString()
            : null;
        var subjectRaw = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("subject", out var sEl) && sEl.ValueKind == JsonValueKind.String
            ? sEl.GetString()
            : null;

        return new DraftAssessment
        {
            Title = (string.IsNullOrEmpty(titleRaw) ? "AI Generated Assessment" : titleRaw)!.Trim(),
            Description = (descRaw ?? "").Trim(),
            Subject = (string.IsNullOrEmpty(subjectRaw) ? fallbackSubject : subjectRaw)!.Trim(),
            DurationMinutes = minutes,
            Questions = questions,
        };
    }

    public static string BuildPrompt(string mode, string input, int count, string difficulty)
    {
        const string schema = """
{
  "title": string (specific, exam-worthy),
  "description": string (1-2 sentences for students),
  "subject": string (short discipline name),
  "questions": [
    {
      "text": string (clear, self-contained),
      "topic": string (2-4 word concept tag),
      "explanation": string (why the correct option is correct, 2-3 sentences),
      "options": [
        { "text": string, "correct": true, "whyWrong": "" },
        { "text": string, "correct": false, "whyWrong": string (why THIS option is wrong — the misconception it represents) },
        { "text": string, "correct": false, "whyWrong": string },
        { "text": string, "correct": false, "whyWrong": string }
      ]
    }
  ]
}
""";

        if (mode == "topic")
        {
            return "Research the topic \"" + input + "\" from your knowledge — cover its core concepts, common misconceptions, and " + difficulty + "-level applications. Then author an assessment of exactly " + count + " questions.\n" +
                "Rules:\n" +
                "- Exactly 4 options per question, exactly 1 correct.\n" +
                "- Vary which position is correct across questions.\n" +
                "- Difficulty: " + difficulty + ". Mix conceptual, applied, and edge-case questions.\n" +
                "- Tag each question with a specific \"topic\" (2-4 words) so analytics can group performance.\n" +
                "- Every wrong option needs a specific, educational \"whyWrong\".\n" +
                "Return JSON matching this schema exactly:\n" +
                schema;
        }

        return "You are given SOURCE MATERIAL below. Use it as the ONLY knowledge base. Author an assessment of exactly " + count + " questions at " + difficulty + " difficulty that probes comprehension of this material (definitions, mechanisms, implications, comparisons found in it). Do not ask about anything not present in the material.\n" +
            "Rules:\n" +
            "- Exactly 4 options per question, exactly 1 correct.\n" +
            "- Distractors must be plausible misreadings OF THE MATERIAL, each with a specific \"whyWrong\".\n" +
            "- Tag each question with a \"topic\" drawn from the material's sections/themes.\n" +
            "Return JSON matching this schema exactly:\n" +
            schema + "\n" +
            "\n" +
            "SOURCE MATERIAL:\n" +
            "\"\"\"\n" +
            input + "\n" +
            "\"\"\"";
    }
}
