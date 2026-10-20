using System.Text.Json;
using Ascend.Api.Data;
using Ascend.Api.Events;
using Ascend.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

[Route("api/assessments")]
public class AssessmentsController : ApiControllerBase
{
    private readonly IAscendEventPublisher _events;

    public AssessmentsController(IOptions<JwtOptions> jwt, IAscendEventPublisher events)
        : base(jwt) => _events = events;

    private static string InClause(IReadOnlyList<string> ids) =>
        string.Join(",", Enumerable.Range(0, ids.Count).Select(i => $"@p{i}"));

    private static DynamicParameters InParams(IReadOnlyList<string> ids)
    {
        var p = new DynamicParameters();
        for (var i = 0; i < ids.Count; i++) p.Add($"p{i}", ids[i]);
        return p;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        await using var conn = await Db.OpenConnectionAsync();

        if (user.Role == "teacher")
        {
            var rows = (await conn.QueryAsync<AssessmentRow>(
                "SELECT * FROM assessments WHERE teacher_id = @id ORDER BY created_at DESC",
                new { id = user.Id.ToString() })).AsList();
            var ids = rows.Select(r => r.Id.ToString()).ToArray();
            var qCounts = ids.Length > 0
                ? (await conn.QueryAsync<QuestionRow>($"SELECT * FROM questions WHERE assessment_id IN ({InClause(ids)})", InParams(ids))).AsList()
                : new List<QuestionRow>();
            var atts = ids.Length > 0
                ? (await conn.QueryAsync<AttemptRow>($"SELECT * FROM attempts WHERE assessment_id IN ({InClause(ids)})", InParams(ids))).AsList()
                : new List<AttemptRow>();

            var payload = rows.Select(a =>
            {
                var sub = atts.Where(t => t.AssessmentId == a.Id && t.Status == "submitted").ToList();
                int? avg = sub.Count > 0
                    ? JsRound(sub.Sum(t => t.TotalQuestions > 0 ? (double)t.CorrectCount / t.TotalQuestions : 0) / sub.Count * 100)
                    : null;
                return (object)new
                {
                    id = a.Id,
                    teacherId = a.TeacherId,
                    title = a.Title,
                    description = a.Description,
                    subject = a.Subject,
                    durationMinutes = a.DurationMinutes,
                    published = a.Published,
                    isAiGenerated = a.IsAiGenerated,
                    createdAt = a.CreatedAt,
                    questionCount = qCounts.Count(q => q.AssessmentId == a.Id),
                    submissionCount = sub.Count,
                    avgScore = avg,
                };
            }).ToList();

            return Json(new { assessments = payload });
        }

        var studentRows = (await conn.QueryAsync<StudentAssessmentRow>(
            "SELECT a.id, a.title, a.description, a.subject, a.duration_minutes, a.is_ai_generated, a.created_at, u.name AS teacher_name " +
            // "WHERE published" is valid in SQLite but not in SQL Server: a bit
            // column is not a boolean predicate there. Use an explicit comparison
            // so the same query works on both providers.
            "FROM assessments a INNER JOIN users u ON a.teacher_id = u.id WHERE a.published = 1 ORDER BY a.created_at DESC")).AsList();
        var sIds = studentRows.Select(r => r.Id.ToString()).ToArray();
        var sQCounts = sIds.Length > 0
            ? (await conn.QueryAsync<QuestionRow>($"SELECT * FROM questions WHERE assessment_id IN ({InClause(sIds)})", InParams(sIds))).AsList()
            : new List<QuestionRow>();
        var mine = (await conn.QueryAsync<AttemptRow>(
            "SELECT * FROM attempts WHERE student_id = @id", new { id = user.Id.ToString() })).AsList();

        var studentPayload = studentRows.Select(a =>
        {
            var m = mine.Where(t => t.AssessmentId == a.Id).ToList();
            var inProgress = m.FirstOrDefault(t => t.Status == "in_progress");
            var submitted = m.Where(t => t.Status == "submitted").ToList();
            double? best = submitted.Count > 0
                ? submitted.Max(t => t.TotalQuestions > 0 ? (double)t.CorrectCount / t.TotalQuestions : 0)
                : null;
            var last = submitted.OrderByDescending(t => t.SubmittedAt ?? DateTime.UnixEpoch).FirstOrDefault();
            return (object)new
            {
                id = a.Id,
                title = a.Title,
                description = a.Description,
                subject = a.Subject,
                durationMinutes = a.DurationMinutes,
                isAiGenerated = a.IsAiGenerated,
                createdAt = a.CreatedAt,
                teacherName = a.TeacherName,
                questionCount = sQCounts.Count(q => q.AssessmentId == a.Id),
                inProgressAttemptId = inProgress?.Id,
                attemptCount = submitted.Count,
                bestScore = best == null ? (int?)null : JsRound(best.Value * 100),
                lastAttemptId = last?.Id,
            };
        }).ToList();

        return Json(new { assessments = studentPayload });
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);

        try
        {
            var body = await ReadBodyAsync();
            if (body == null) return Err("Failed to save assessment.", 500);
            var validationError = ValidateDraft(body.Value);
            if (validationError != null) return Err(validationError, 400);

            var title = GetStr(body, "title")!.Trim();
            var description = (GetStr(body, "description") ?? "").Trim();
            var subjectRaw = GetStr(body, "subject");
            var subject = subjectRaw == null ? "General" : subjectRaw.Trim();
            var duration = JsRound(GetNum(body, "durationMinutes")!.Value);
            var isAiGenerated = GetBool(body, "isAiGenerated");
            var questionsEl = body.Value.GetProperty("questions");

            await using var conn = await Db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            var assessmentId = Db.NewId();
            await conn.ExecuteAsync(
                "INSERT INTO assessments (id, teacher_id, title, description, subject, duration_minutes, published, is_ai_generated, created_at) " +
                "VALUES (@id, @teacherId, @title, @description, @subject, @durationMinutes, 1, @isAiGenerated, @now)",
                new { id = assessmentId, teacherId = user.Id.ToString(), title, description, subject, durationMinutes = duration, isAiGenerated, now = Db.NowDb() }, tx);
            var a = await conn.QuerySingleAsync<AssessmentRow>(
                "SELECT * FROM assessments WHERE id = @id", new { id = assessmentId }, tx);

            var i = 0;
            foreach (var q in questionsEl.EnumerateArray())
            {
                var qText = GetStr(q, "text")!.Trim();
                var topicRaw = GetStr(q, "topic");
                var topic = string.IsNullOrEmpty(topicRaw) || topicRaw.Trim().Length == 0 ? "General" : topicRaw.Trim();
                var explanation = (GetStr(q, "explanation") ?? "").Trim();

                var questionId = Db.NewId();
                await conn.ExecuteAsync(
                    "INSERT INTO questions (id, assessment_id, idx, text, topic, correct_explanation, created_at) VALUES (@id, @assessmentId, @idx, @text, @topic, @correctExplanation, @now)",
                    new { id = questionId, assessmentId = a.Id.ToString(), idx = i, text = qText, topic, correctExplanation = explanation, now = Db.NowDb() }, tx);
                var qRow = await conn.QuerySingleAsync<QuestionRow>(
                    "SELECT * FROM questions WHERE id = @id", new { id = questionId }, tx);

                var j = 0;
                foreach (var o in q.GetProperty("options").EnumerateArray())
                {
                    var oText = GetStr(o, "text")!.Trim();
                    var isCorrect = o.TryGetProperty("correct", out var cEl) && AiClient.JsTruthy(cEl);
                    var whyWrong = (GetStr(o, "whyWrong") ?? "").Trim();
                    await conn.ExecuteAsync(
                        "INSERT INTO options (id, question_id, idx, text, is_correct, wrong_explanation) VALUES (@id, @questionId, @idx, @text, @isCorrect, @wrongExplanation)",
                        new { id = Db.NewId(), questionId = qRow.Id.ToString(), idx = j, text = oText, isCorrect, wrongExplanation = whyWrong }, tx);
                    j++;
                }
                i++;
            }

            await tx.CommitAsync();
            _events.Publish(AscendEventTypes.AssessmentCreated, a.Id.ToString(), new { id = a.Id, teacherId = user.Id, title, subject, durationMinutes = duration, questionCount = i });
            return Json(new { ok = true, id = a.Id });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[assessments:create] {ex}");
            return Err("Failed to save assessment.", 500);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (!Guid.TryParse(id, out var aid)) return Err("Assessment not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var a = await conn.QueryFirstOrDefaultAsync<AssessmentWithTeacherRow>(
            "SELECT a.*, u.name AS teacher_name FROM assessments a INNER JOIN users u ON a.teacher_id = u.id WHERE a.id = @aid",
            new { aid = aid.ToString() });
        if (a == null) return Err("Assessment not found.", 404);

        var isOwner = a.TeacherId == user.Id;
        if (!isOwner && !a.Published) return Err("Assessment not found.", 404);

        var qs = (await conn.QueryAsync<QuestionRow>(
            "SELECT * FROM questions WHERE assessment_id = @aid ORDER BY idx ASC",
            new { aid = aid.ToString() })).AsList();
        var qids = qs.Select(q => q.Id.ToString()).ToArray();
        var opts = qids.Length > 0
            ? (await conn.QueryAsync<OptionRow>($"SELECT * FROM options WHERE question_id IN ({InClause(qids)}) ORDER BY idx ASC", InParams(qids))).AsList()
            : new List<OptionRow>();

        var questions = qs.Select(q =>
        {
            var optionList = opts.Where(o => o.QuestionId == q.Id)
                .Select(o => (object)new { id = o.Id, idx = o.Idx, text = o.Text })
                .ToList();
            return isOwner
                ? (object)new { id = q.Id, idx = q.Idx, text = q.Text, topic = q.Topic, options = optionList }
                : (object)new { id = q.Id, idx = q.Idx, text = q.Text, options = optionList };
        }).ToList();

        var attempts = new List<object>();
        if (user.Role == "student")
        {
            var mine = await conn.QueryAsync<AttemptRow>(
                "SELECT * FROM attempts WHERE assessment_id = @aid AND student_id = @sid",
                new { aid = aid.ToString(), sid = user.Id.ToString() });
            attempts = mine.Select(t => (object)new
            {
                id = t.Id,
                status = t.Status,
                correctCount = t.CorrectCount,
                totalQuestions = t.TotalQuestions,
                submittedAt = t.SubmittedAt,
                startedAt = t.StartedAt,
            }).ToList();
        }
        else if (isOwner)
        {
            var all = await conn.QueryAsync<AttemptWithStudentRow>(
                "SELECT t.id, t.status, t.correct_count, t.total_questions, t.submitted_at, t.time_taken_seconds, u.name AS student_name " +
                "FROM attempts t INNER JOIN users u ON t.student_id = u.id WHERE t.assessment_id = @aid",
                new { aid = aid.ToString() });
            attempts = all.Select(t => (object)new
            {
                id = t.Id,
                status = t.Status,
                correctCount = t.CorrectCount,
                totalQuestions = t.TotalQuestions,
                submittedAt = t.SubmittedAt,
                timeTakenSeconds = t.TimeTakenSeconds,
                studentName = t.StudentName,
            }).ToList();
        }

        return Json(new
        {
            assessment = new
            {
                id = a.Id,
                teacherId = a.TeacherId,
                title = a.Title,
                description = a.Description,
                subject = a.Subject,
                durationMinutes = a.DurationMinutes,
                published = a.Published,
                isAiGenerated = a.IsAiGenerated,
                createdAt = a.CreatedAt,
                teacherName = a.TeacherName,
                questions,
            },
            attempts,
            isOwner,
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await CurrentUser();
        if (user == null) return Err("Unauthorized", 401);
        if (!Guid.TryParse(id, out var aid)) return Err("Not found.", 404);

        await using var conn = await Db.OpenConnectionAsync();
        var a = await conn.QueryFirstOrDefaultAsync<AssessmentRow>(
            "SELECT * FROM assessments WHERE id = @aid", new { aid = aid.ToString() });
        if (a == null || a.TeacherId != user.Id) return Err("Not found.", 404);

        await conn.ExecuteAsync("DELETE FROM assessments WHERE id = @aid", new { aid = aid.ToString() });
        _events.Publish(AscendEventTypes.AssessmentDeleted, aid.ToString(), new { id = aid, teacherId = user.Id });
        return Json(new { ok = true });
    }

    private static string? ValidateDraft(JsonElement body)
    {
        var title = GetStr(body, "title");
        if (string.IsNullOrEmpty(title) || title.Trim().Length < 4)
            return "Give the assessment a title (at least 4 characters).";
        if (body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("questions", out var qs) ||
            qs.ValueKind != JsonValueKind.Array ||
            qs.GetArrayLength() == 0)
            return "Add at least one question.";
        var duration = GetNum(body, "durationMinutes");
        if (duration == null || duration < 1 || duration > 300)
            return "Duration must be between 1 and 300 minutes.";

        var index = 0;
        foreach (var q in qs.EnumerateArray())
        {
            index++;
            var qText = q.ValueKind == JsonValueKind.Object ? GetStr(q, "text") : null;
            if (string.IsNullOrEmpty(qText) || qText.Trim().Length < 6)
                return $"Question {index} needs text.";
            if (q.ValueKind != JsonValueKind.Object ||
                !q.TryGetProperty("options", out var opts) ||
                opts.ValueKind != JsonValueKind.Array ||
                opts.GetArrayLength() < 2 ||
                opts.GetArrayLength() > 6)
                return $"Question {index} must have 2–6 options.";
            var correct = opts.EnumerateArray().Count(o => o.ValueKind == JsonValueKind.Object && o.TryGetProperty("correct", out var c) && AiClient.JsTruthy(c));
            if (correct != 1)
                return $"Question {index} must have exactly one correct option.";
            foreach (var o in opts.EnumerateArray())
            {
                var oText = o.ValueKind == JsonValueKind.Object ? GetStr(o, "text") : null;
                if (string.IsNullOrEmpty(oText) || oText.Trim().Length == 0)
                    return $"Question {index} has an empty option.";
            }
        }
        return null;
    }
}
