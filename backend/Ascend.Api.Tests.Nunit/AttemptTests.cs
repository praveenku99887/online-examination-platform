using System.Text.Json;
using Ascend.Api.Tests.Shared;
using NUnit.Framework;

namespace Ascend.Api.Tests.Nunit;

[TestFixture]
public sealed class AttemptTests
{
    private static async Task<ApiClient> SignupAsync(string role)
    {
        var api = new ApiClient(GlobalSetup.Factory);
        var response = await api.SignupAsync(
            role == "teacher" ? "Test Teacher" : "Test Student",
            TestData.UniqueEmail(role),
            role: role);
        Assert.That(response.StatusCode, Is.EqualTo(200));
        return api;
    }

    private static async Task<(Guid attemptId, JsonElement session)> StartAttemptAsync(
        ApiClient teacher,
        ApiClient student)
    {
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        Assert.That(created.StatusCode, Is.EqualTo(200));
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var start = await student.StartAttemptAsync(assessmentId);
        Assert.That(start.StatusCode, Is.EqualTo(200));
        var attemptId = start.Body.GetProperty("attemptId").GetGuid();

        var session = await student.GetAsync($"/api/attempts/{attemptId}");
        Assert.That(session.StatusCode, Is.EqualTo(200));
        return (attemptId, session.Body);
    }

    private static (Dictionary<string, string> answers, Dictionary<string, int> timings)
        CorrectThenWrong(JsonElement session)
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

    [Test]
    public async Task Teacher_cannot_start_an_attempt()
    {
        using var teacher = await SignupAsync("teacher");
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var response = await teacher.StartAttemptAsync(assessmentId);

        Assert.That(response.StatusCode, Is.EqualTo(403));
        Assert.That(response.Error, Is.EqualTo("Only students can attempt assessments."));
    }

    [Test]
    public async Task Start_resumes_an_open_attempt()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var created = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        var assessmentId = created.Body.GetProperty("id").GetGuid();

        var first = await student.StartAttemptAsync(assessmentId);
        var second = await student.StartAttemptAsync(assessmentId);

        Assert.That(first.StatusCode, Is.EqualTo(200));
        Assert.That(first.Body.GetProperty("resumed").GetBoolean(), Is.False);
        Assert.That(second.Body.GetProperty("resumed").GetBoolean(), Is.True);
        Assert.That(
            second.Body.GetProperty("attemptId").GetGuid(),
            Is.EqualTo(first.Body.GetProperty("attemptId").GetGuid()));
    }

    [Test]
    public async Task Submit_scores_and_report_contains_per_question_detail()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var (attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);

        var submit = await student.SubmitAttemptAsync(attemptId, answers, timings);
        var report = await student.ReportAsync(attemptId);

        Assert.That(submit.StatusCode, Is.EqualTo(200));
        Assert.That(submit.Body.GetProperty("correctCount").GetInt32(), Is.EqualTo(1));
        Assert.That(submit.Body.GetProperty("total").GetInt32(), Is.EqualTo(2));

        Assert.That(report.StatusCode, Is.EqualTo(200));
        var attempt = report.Body.GetProperty("attempt");
        Assert.That(attempt.GetProperty("pct").GetInt32(), Is.EqualTo(50));
        Assert.That(attempt.GetProperty("studentName").GetString(), Is.EqualTo("Test Student"));

        var questions = report.Body.GetProperty("questions").EnumerateArray().ToArray();
        Assert.That(questions.Length, Is.EqualTo(2));
        Assert.That(questions[0].GetProperty("isCorrect").GetBoolean(), Is.True);
        Assert.That(questions[1].GetProperty("isCorrect").GetBoolean(), Is.False);
        Assert.That(questions[0].GetProperty("topic").GetString(), Is.EqualTo("Astronomy"));
        Assert.That(
            questions[0].GetProperty("correctExplanation").GetString(),
            Is.Not.Null.And.Not.Empty);

        foreach (var option in questions[0].GetProperty("options").EnumerateArray())
        {
            Assert.That(option.TryGetProperty("isCorrect", out _), Is.True);
            Assert.That(option.TryGetProperty("wrongExplanation", out _), Is.True);
        }
    }

    [Test]
    public async Task Submit_after_expiry_returns_410()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var (attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);

        await TestData.ExpireAttemptAsync(attemptId);

        var submit = await student.SubmitAttemptAsync(attemptId, answers, timings);

        Assert.That(submit.StatusCode, Is.EqualTo(410));
        Assert.That(
            submit.Error,
            Is.EqualTo("Time expired; attempt can no longer be submitted."));
    }

    [Test]
    public async Task Teacher_can_read_a_students_report()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var (attemptId, session) = await StartAttemptAsync(teacher, student);
        var (answers, timings) = CorrectThenWrong(session);
        await student.SubmitAttemptAsync(attemptId, answers, timings);

        var report = await teacher.ReportAsync(attemptId);

        Assert.That(report.StatusCode, Is.EqualTo(200));
        Assert.That(
            report.Body.GetProperty("attempt").GetProperty("studentName").GetString(),
            Is.EqualTo("Test Student"));
    }

    [Test]
    public async Task Auto_submitted_flag_is_persisted()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var (attemptId, _) = await StartAttemptAsync(teacher, student);

        await student.SubmitAttemptAsync(
            attemptId, new Dictionary<string, string>(), auto: true);
        var report = await student.ReportAsync(attemptId);

        Assert.That(
            report.Body.GetProperty("attempt").GetProperty("autoSubmitted").GetBoolean(),
            Is.True);
    }
}
