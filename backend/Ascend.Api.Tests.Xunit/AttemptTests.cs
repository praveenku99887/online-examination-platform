using System.Text.Json;
using Ascend.Api.Tests.Shared;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

[Collection(ApiCollection.Name)]
public sealed class AttemptTests
{
    private readonly AscendWebApplicationFactory _factory;

    public AttemptTests(AscendWebApplicationFactory factory) => _factory = factory;

    private static async Task<ApiClient> SignupAsync(
        AscendWebApplicationFactory factory,
        string role,
        string? name = null)
    {
        var api = new ApiClient(factory);
        var response = await api.SignupAsync(
            name ?? (role == "teacher" ? "Test Teacher" : "Test Student"),
            TestData.UniqueEmail(role),
            role: role);
        Assert.Equal(200, response.StatusCode);
        return api;
    }

    private static async Task<(Guid assessmentId, Guid attemptId, JsonElement session)>
        StartAttemptAsync(ApiClient teacher, ApiClient student)
    {
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        Assert.Equal(200, created.StatusCode);
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var start = await student.StartAttemptAsync(assessmentId);
        Assert.Equal(200, start.StatusCode);
        var attemptId = start.Body.GetProperty("attemptId").GetGuid();

        var session = await student.GetAsync($"/api/attempts/{attemptId}");
        Assert.Equal(200, session.StatusCode);

        return (assessmentId, attemptId, session.Body);
    }

    private static (
        Dictionary<string, string> answers,
        Dictionary<string, int> timings) CorrectThenWrong(JsonElement session)
    {
        var questions = session.GetProperty("assessment")
            .GetProperty("questions")
            .EnumerateArray()
            .ToArray();

        var first = questions[0];
        var second = questions[1];
        var firstOptions = first.GetProperty("options").EnumerateArray().ToArray();
        var secondOptions = second.GetProperty("options").EnumerateArray().ToArray();

        var answers = new Dictionary<string, string>
        {
            [first.GetProperty("id").GetString()!] =
                firstOptions[0].GetProperty("id").GetString()!,
            [second.GetProperty("id").GetString()!] =
                secondOptions[1].GetProperty("id").GetString()!,
        };

        var timings = new Dictionary<string, int>
        {
            [first.GetProperty("id").GetString()!] = 5,
            [second.GetProperty("id").GetString()!] = 7,
        };

        return (answers, timings);
    }

    [Fact]
    public async Task Teacher_cannot_start_an_attempt()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var response = await teacher.StartAttemptAsync(assessmentId);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal("Only students can attempt assessments.", response.Error);
    }

    [Fact]
    public async Task Start_resumes_the_same_open_attempt()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var first = await student.StartAttemptAsync(assessmentId);
        var second = await student.StartAttemptAsync(assessmentId);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.False(first.Body.GetProperty("resumed").GetBoolean());
        Assert.True(second.Body.GetProperty("resumed").GetBoolean());
        Assert.Equal(
            first.Body.GetProperty("attemptId").GetGuid(),
            second.Body.GetProperty("attemptId").GetGuid());
    }

    [Fact]
    public async Task Submit_scores_answers_and_report_contains_detail()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);

        var submit = await student.SubmitAttemptAsync(attemptId, answers, timings);

        Assert.Equal(200, submit.StatusCode);
        Assert.Equal(1, submit.Body.GetProperty("correctCount").GetInt32());
        Assert.Equal(2, submit.Body.GetProperty("total").GetInt32());

        var report = await student.ReportAsync(attemptId);
        Assert.Equal(200, report.StatusCode);

        var attempt = report.Body.GetProperty("attempt");
        Assert.Equal(1, attempt.GetProperty("correctCount").GetInt32());
        Assert.Equal(2, attempt.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(50, attempt.GetProperty("pct").GetInt32());
        Assert.Equal("Test Student", attempt.GetProperty("studentName").GetString());
        Assert.False(attempt.GetProperty("autoSubmitted").GetBoolean());

        var questions = report.Body.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal(2, questions.Length);

        var first = questions[0];
        Assert.True(first.GetProperty("isCorrect").GetBoolean());
        Assert.Equal("Astronomy", first.GetProperty("topic").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            first.GetProperty("correctExplanation").GetString()));

        foreach (var option in first.GetProperty("options").EnumerateArray())
        {
            Assert.True(option.TryGetProperty("isCorrect", out _));
            Assert.True(option.TryGetProperty("wrongExplanation", out _));
        }

        var second = questions[1];
        Assert.False(second.GetProperty("isCorrect").GetBoolean());
        var chosenId = second.GetProperty("chosenId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(chosenId));

        var chosenOption = second.GetProperty("options").EnumerateArray()
            .First(o => o.GetProperty("id").GetString() == chosenId);
        Assert.False(string.IsNullOrWhiteSpace(
            chosenOption.GetProperty("wrongExplanation").GetString()));

        var peers = report.Body.GetProperty("peers");
        Assert.Equal(0, peers.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, peers.GetProperty("percentile").ValueKind);
    }

    [Fact]
    public async Task Submit_ignores_option_ids_that_do_not_belong_to_the_question()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);

        var questions = session.GetProperty("assessment")
            .GetProperty("questions")
            .EnumerateArray()
            .ToArray();
        var answers = questions.ToDictionary(
            q => q.GetProperty("id").GetString()!,
            _ => Guid.NewGuid().ToString());

        var submit = await student.SubmitAttemptAsync(attemptId, answers);

        Assert.Equal(200, submit.StatusCode);
        Assert.Equal(0, submit.Body.GetProperty("correctCount").GetInt32());

        var report = await student.ReportAsync(attemptId);
        foreach (var question in report.Body.GetProperty("questions").EnumerateArray())
            Assert.Equal(JsonValueKind.Null, question.GetProperty("chosenId").ValueKind);
    }

    [Fact]
    public async Task Submit_after_expiry_returns_410()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);

        await TestData.ExpireAttemptAsync(attemptId);

        var submit = await student.SubmitAttemptAsync(attemptId, answers, timings);

        Assert.Equal(410, submit.StatusCode);
        Assert.Equal("Time expired; attempt can no longer be submitted.", submit.Error);
    }

    [Fact]
    public async Task Submitting_twice_is_idempotent()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);

        var first = await student.SubmitAttemptAsync(attemptId, answers, timings);
        var second = await student.SubmitAttemptAsync(attemptId, answers, timings);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.True(second.Body.GetProperty("alreadySubmitted").GetBoolean());

        var report = await student.ReportAsync(attemptId);
        Assert.Equal(
            1,
            report.Body.GetProperty("attempt").GetProperty("correctCount").GetInt32());
    }

    [Fact]
    public async Task Report_before_submission_returns_409()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, _) = await StartAttemptAsync(teacher, student);

        var report = await student.ReportAsync(attemptId);

        Assert.Equal(409, report.StatusCode);
        Assert.Equal("Attempt not submitted yet.", report.Error);
    }

    [Fact]
    public async Task Session_after_submission_returns_409()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, _) = await StartAttemptAsync(teacher, student);

        var submit = await student.SubmitAttemptAsync(
            attemptId, new Dictionary<string, string>());
        Assert.Equal(200, submit.StatusCode);

        var session = await student.GetAsync($"/api/attempts/{attemptId}");

        Assert.Equal(409, session.StatusCode);
        Assert.Equal("Attempt already submitted.", session.Error);
    }

    [Fact]
    public async Task Another_student_cannot_read_the_attempt_or_report()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        using var otherStudent = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);
        await student.SubmitAttemptAsync(attemptId, answers, timings);

        var attempt = await otherStudent.GetAsync($"/api/attempts/{attemptId}");
        var report = await otherStudent.ReportAsync(attemptId);

        Assert.Equal(403, attempt.StatusCode);
        Assert.Equal(403, report.StatusCode);
    }

    [Fact]
    public async Task Teacher_can_read_a_students_report()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);
        await student.SubmitAttemptAsync(attemptId, answers, timings);

        var report = await teacher.ReportAsync(attemptId);

        Assert.Equal(200, report.StatusCode);
        Assert.Equal(
            "Test Student",
            report.Body.GetProperty("attempt").GetProperty("studentName").GetString());
    }

    [Fact]
    public async Task Auto_submitted_flag_is_persisted()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var (_, attemptId, _) = await StartAttemptAsync(teacher, student);

        var submit = await student.SubmitAttemptAsync(
            attemptId, new Dictionary<string, string>(), auto: true);
        Assert.Equal(200, submit.StatusCode);

        var report = await student.ReportAsync(attemptId);
        Assert.True(report.Body
            .GetProperty("attempt")
            .GetProperty("autoSubmitted")
            .GetBoolean());
    }
}
