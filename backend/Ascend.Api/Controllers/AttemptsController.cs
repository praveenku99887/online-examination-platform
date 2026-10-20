using System.Text.Json;
using Ascend.Api.Data;
using Ascend.Api.Events;
using Ascend.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

[Route("api/attempts")]
public class AttemptsController : ApiControllerBase
{
    private readonly IAscendEventPublisher _events;

    public AttemptsController(IOptions<JwtOptions> jwt, IAscendEventPublisher events)
        : base(jwt) => _events = events;

    private static string InClause(IReadOnlyList<string> ids) =>
        string.Join(",", Enumerable.Range(0, ids.Count).Select(i => $"@p{i}"));

    private static DynamicParameters InParams(IReadOnlyList<string> ids)
    {
        var p = new DynamicParameters();
        for (var i = 0; i < ids.Count; i++) p.Add($"p{i}", ids[i]);
        return p;
    }

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (user.Role != "student")
            return Err("Only students can attempt assessments.", 403);

        var body = await ReadBodyAsync();
        var assessmentIdRaw = GetStr(body, "assessmentId");
        if (string.IsNullOrEmpty(assessmentIdRaw))
            return Err("assessmentId is required.", 400);
        if (!Guid.TryParse(assessmentIdRaw, out var assessmentId))
            return Err("Assessment not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var a = await conn.QueryFirstOrDefaultAsync<AssessmentRow>(
            "SELECT * FROM assessments WHERE id = @assessmentId", new { assessmentId = assessmentId.ToString() });
        if (a == null || !a.Published)
            return Err("Assessment not found.", 404);

        var open = await conn.QueryFirstOrDefaultAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE assessment_id = @assessmentId AND student_id = @sid AND status = 'in_progress' AND expires_at > @now",
            new { assessmentId = assessmentId.ToString(), sid = user.Id.ToString(), now = Db.NowDb() });
        if (open != null)
            return Json(new { ok = true, attemptId = open.Id, resumed = true });

        var questionIds = (await conn.QueryAsync<string>(
            "SELECT id FROM questions WHERE assessment_id = @assessmentId",
            new { assessmentId = assessmentId.ToString() })).AsList();
        if (questionIds.Count == 0)
            return Err("This assessment has no questions yet.", 400);

        var expiresAt = Clock.UtcNowMs().AddMinutes(a.DurationMinutes);
        var attemptId = Db.NewId();
        await conn.ExecuteAsync(
            "INSERT INTO attempts (id, assessment_id, student_id, status, started_at, expires_at, total_questions) " +
            "VALUES (@id, @assessmentId, @sid, 'in_progress', @startedAt, @expiresAt, @totalQuestions)",
            new { id = attemptId, assessmentId = assessmentId.ToString(), sid = user.Id.ToString(), startedAt = Db.NowDb(), expiresAt = Db.ToDb(expiresAt), totalQuestions = questionIds.Count });
        var attempt = await conn.QuerySingleAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE id = @id", new { id = attemptId });

        return Json(new { ok = true, attemptId = attempt.Id, resumed = false });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Session(string id)
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (!Guid.TryParse(id, out var tid)) return Err("Attempt not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var t = await conn.QueryFirstOrDefaultAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE id = @tid", new { tid = tid.ToString() });
        if (t == null) return Err("Attempt not found.", 404);

        var a = await conn.QueryFirstOrDefaultAsync<AssessmentRow>(
            "SELECT * FROM assessments WHERE id = @aid", new { aid = t.AssessmentId.ToString() });
        if (a == null) return Err("Assessment not found.", 404);

        var isOwner = t.StudentId == user.Id;
        var isTeacher = a.TeacherId == user.Id;
        if (!isOwner && !isTeacher) return Err("Forbidden.", 403);

        if (t.Status == "submitted")
            return StatusCode(409, new { error = "Attempt already submitted.", report = true });

        var qs = (await conn.QueryAsync<QuestionRow>(
            "SELECT * FROM questions WHERE assessment_id = @aid ORDER BY idx ASC",
            new { aid = a.Id.ToString() })).AsList();
        var qids = qs.Select(q => q.Id.ToString()).ToArray();
        var opts = qids.Length > 0
            ? (await conn.QueryAsync<OptionRow>($"SELECT * FROM options WHERE question_id IN ({InClause(qids)}) ORDER BY idx ASC", InParams(qids))).AsList()
            : new List<OptionRow>();

        var questions = qs.Select(q => (object)new
        {
            id = q.Id,
            idx = q.Idx,
            text = q.Text,
            options = opts.Where(o => o.QuestionId == q.Id)
                .Select(o => (object)new { id = o.Id, idx = o.Idx, text = o.Text })
                .ToList(),
        }).ToList();

        return Json(new
        {
            attempt = new
            {
                id = t.Id,
                startedAt = t.StartedAt,
                expiresAt = t.ExpiresAt,
                answers = ToJsonElement(t.Answers),
                timings = ToJsonElement(t.Timings),
                serverNow = Clock.Iso(Clock.UtcNowMs()),
            },
            assessment = new
            {
                id = a.Id,
                title = a.Title,
                subject = a.Subject,
                durationMinutes = a.DurationMinutes,
                questions,
            },
        });
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(string id)
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (!Guid.TryParse(id, out var tid)) return Err("Attempt not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var t = await conn.QueryFirstOrDefaultAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE id = @tid", new { tid = tid.ToString() });
        if (t == null) return Err("Attempt not found.", 404);
        if (t.StudentId != user.Id) return Err("Forbidden.", 403);
        if (t.Status == "submitted")
            return Json(new { ok = true, attemptId = t.Id, alreadySubmitted = true });

        var body = await ReadBodyAsync();
        var now = Clock.UtcNowMs();
        if (now > t.ExpiresAt.AddMilliseconds(90_000))
            return Err("Time expired; attempt can no longer be submitted.", 410);

        var answers = new Dictionary<string, string>();
        if (body is JsonElement bel && bel.ValueKind == JsonValueKind.Object &&
            bel.TryGetProperty("answers", out var answersEl) && answersEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in answersEl.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String)
                    answers[p.Name] = p.Value.GetString()!;
        }

        var timingsRaw = "{}";
        if (body is JsonElement b2 && b2.ValueKind == JsonValueKind.Object &&
            b2.TryGetProperty("timings", out var timingsEl) && timingsEl.ValueKind == JsonValueKind.Object)
        {
            timingsRaw = timingsEl.GetRawText();
        }

        var auto = body is JsonElement b3 && b3.ValueKind == JsonValueKind.Object &&
                   b3.TryGetProperty("auto", out var autoEl) && AiClient.JsTruthy(autoEl);

        var qs = (await conn.QueryAsync<QuestionRow>(
            "SELECT * FROM questions WHERE assessment_id = @aid", new { aid = t.AssessmentId.ToString() })).AsList();
        var qids = qs.Select(q => q.Id.ToString()).ToArray();
        var opts = qids.Length > 0
            ? (await conn.QueryAsync<OptionRow>($"SELECT * FROM options WHERE question_id IN ({InClause(qids)})", InParams(qids))).AsList()
            : new List<OptionRow>();

        var correct = 0;
        var cleaned = new Dictionary<string, string>();
        foreach (var q in qs)
        {
            var qidStr = q.Id.ToString();
            if (!answers.TryGetValue(qidStr, out var chosen)) continue;
            var valid = opts.Any(o => o.QuestionId == q.Id && o.Id.ToString() == chosen);
            if (!valid) continue;
            cleaned[qidStr] = chosen;
            var correctOpt = opts.FirstOrDefault(o => o.QuestionId == q.Id && o.IsCorrect);
            if (correctOpt != null && correctOpt.Id.ToString() == chosen) correct++;
        }

        var elapsed = (now - t.StartedAt).TotalSeconds;
        var window = (t.ExpiresAt - t.StartedAt).TotalSeconds;
        var timeTaken = Math.Min(Math.Max(1, JsRound(elapsed)), JsRound(window));

        await conn.ExecuteAsync(
            "UPDATE attempts SET status = 'submitted', submitted_at = @now, answers = @answers, timings = @timings, " +
            "correct_count = @correct, total_questions = @total, time_taken_seconds = @timeTaken, auto_submitted = @auto WHERE id = @tid",
            new
            {
                now = Db.ToDb(now),
                answers = JsonSerializer.Serialize(cleaned),
                timings = timingsRaw,
                correct,
                total = qs.Count,
                timeTaken,
                auto,
                tid = tid.ToString(),
            });

        _events.Publish(AscendEventTypes.AttemptSubmitted, t.Id.ToString(), new { id = t.Id, assessmentId = t.AssessmentId, studentId = t.StudentId, correctCount = correct, total = qs.Count, autoSubmitted = auto });
        return Json(new { ok = true, attemptId = t.Id, correctCount = correct, total = qs.Count });
    }

    [HttpGet("{id}/report")]
    public async Task<IActionResult> Report(string id)
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (!Guid.TryParse(id, out var tid)) return Err("Attempt not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var t = await conn.QueryFirstOrDefaultAsync<AttemptWithStudentRow>(
            "SELECT t.*, u.name AS student_name FROM attempts t INNER JOIN users u ON t.student_id = u.id WHERE t.id = @tid",
            new { tid = tid.ToString() });
        if (t == null) return Err("Attempt not found.", 404);

        var a = await conn.QueryFirstOrDefaultAsync<AssessmentRow>(
            "SELECT * FROM assessments WHERE id = @aid", new { aid = t.AssessmentId.ToString() });
        if (a == null) return Err("Assessment not found.", 404);

        var isOwner = t.StudentId == user.Id;
        var isTeacher = a.TeacherId == user.Id;
        if (!isOwner && !isTeacher) return Err("Forbidden.", 403);
        if (t.Status != "submitted") return Err("Attempt not submitted yet.", 409);

        var qs = (await conn.QueryAsync<QuestionRow>(
            "SELECT * FROM questions WHERE assessment_id = @aid ORDER BY idx ASC",
            new { aid = a.Id.ToString() })).AsList();
        var qids = qs.Select(q => q.Id.ToString()).ToArray();
        var opts = qids.Length > 0
            ? (await conn.QueryAsync<OptionRow>($"SELECT * FROM options WHERE question_id IN ({InClause(qids)}) ORDER BY idx ASC", InParams(qids))).AsList()
            : new List<OptionRow>();

        var answersEl = ToJsonElement(t.Answers);
        var timingsEl = ToJsonElement(t.Timings);

        var detail = qs.Select(q =>
        {
            var optionList = opts.Where(o => o.QuestionId == q.Id)
                .Select(o => (object)new
                {
                    id = o.Id,
                    idx = o.Idx,
                    text = o.Text,
                    isCorrect = o.IsCorrect,
                    wrongExplanation = o.WrongExplanation,
                })
                .ToList();

            object? chosenId = null;
            if (answersEl is JsonElement ae && ae.ValueKind == JsonValueKind.Object && ae.TryGetProperty(q.Id.ToString(), out var cv))
                chosenId = cv.Clone();

            var correctOpt = opts.FirstOrDefault(o => o.QuestionId == q.Id && o.IsCorrect);
            Guid? correctId = correctOpt?.Id;
            var isCorrect = chosenId is JsonElement cj && cj.ValueKind == JsonValueKind.String &&
                            correctId != null && cj.GetString() == correctId.Value.ToString();

            object? timeSeconds = null;
            if (timingsEl is JsonElement te && te.ValueKind == JsonValueKind.Object && te.TryGetProperty(q.Id.ToString(), out var tv))
                timeSeconds = tv.Clone();

            return (object)new
            {
                id = q.Id,
                idx = q.Idx,
                text = q.Text,
                topic = q.Topic,
                correctExplanation = q.CorrectExplanation,
                options = optionList,
                chosenId,
                correctId,
                isCorrect,
                timeSeconds,
            };
        }).ToList();

        var peers = (await conn.QueryAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE assessment_id = @aid AND status = 'submitted' AND id != @tid",
            new { aid = a.Id.ToString(), tid = tid.ToString() })).AsList();

        var myPct = t.TotalQuestions > 0 ? JsRound((double)t.CorrectCount / t.TotalQuestions * 100) : 0;
        var peerPcts = peers.Select(p => p.TotalQuestions > 0 ? (double)p.CorrectCount / p.TotalQuestions * 100 : 0).ToList();
        int? peerAvg = peerPcts.Count > 0 ? JsRound(peerPcts.Sum() / peerPcts.Count) : null;
        int? percentile = peerPcts.Count > 0 ? JsRound(peerPcts.Count(p => p <= myPct) / (double)peerPcts.Count * 100) : null;

        return Json(new
        {
            assessment = new
            {
                id = a.Id,
                title = a.Title,
                subject = a.Subject,
                description = a.Description,
                durationMinutes = a.DurationMinutes,
                isAiGenerated = a.IsAiGenerated,
            },
            attempt = new
            {
                id = t.Id,
                studentName = t.StudentName,
                startedAt = t.StartedAt,
                submittedAt = t.SubmittedAt,
                correctCount = t.CorrectCount,
                totalQuestions = t.TotalQuestions,
                timeTakenSeconds = t.TimeTakenSeconds,
                autoSubmitted = t.AutoSubmitted,
                pct = myPct,
            },
            peers = new { count = peers.Count, avgPct = peerAvg, percentile },
            questions = detail,
        });
    }
}
