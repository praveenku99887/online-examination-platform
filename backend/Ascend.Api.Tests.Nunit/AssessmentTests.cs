using System.Text.Json;
using Ascend.Api.Tests.Shared;
using NUnit.Framework;

namespace Ascend.Api.Tests.Nunit;

[TestFixture]
public sealed class AssessmentTests
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

    private static async Task<Guid> CreateAssessmentAsync(ApiClient teacher)
    {
        var response = await teacher.CreateAssessmentAsync(TestData.AssessmentDraft());
        Assert.That(response.StatusCode, Is.EqualTo(200));
        return response.Body.GetProperty("id").GetGuid();
    }

    [Test]
    public async Task Student_list_returns_published_assessments()
    {
        // Regression test for the Azure SQL failure: SQL Server rejects a BIT
        // column used directly as a WHERE predicate.
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await student.GetAsync("/api/assessments");

        Assert.That(response.StatusCode, Is.EqualTo(200));
        var assessments = response.Body.GetProperty("assessments").EnumerateArray().ToArray();
        var item = assessments.FirstOrDefault(
            a => a.GetProperty("id").GetGuid() == assessmentId);

        Assert.That(item.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
        Assert.That(item.GetProperty("questionCount").GetInt32(), Is.EqualTo(2));
        Assert.That(item.GetProperty("bestScore").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Student_get_hides_topic_and_answer_key()
    {
        using var teacher = await SignupAsync("teacher");
        using var student = await SignupAsync("student");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await student.GetAsync($"/api/assessments/{assessmentId}");

        Assert.That(response.StatusCode, Is.EqualTo(200));
        foreach (var question in response.Body
                     .GetProperty("assessment")
                     .GetProperty("questions")
                     .EnumerateArray())
        {
            Assert.That(question.TryGetProperty("topic", out _), Is.False);
            foreach (var option in question.GetProperty("options").EnumerateArray())
                Assert.That(option.TryGetProperty("isCorrect", out _), Is.False);
        }
    }

    [Test]
    public async Task Teacher_get_includes_topic_tags()
    {
        using var teacher = await SignupAsync("teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await teacher.GetAsync($"/api/assessments/{assessmentId}");
        var firstQuestion = response.Body
            .GetProperty("assessment")
            .GetProperty("questions")
            .EnumerateArray()
            .First();

        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Body.GetProperty("isOwner").GetBoolean(), Is.True);
        Assert.That(firstQuestion.GetProperty("topic").GetString(), Is.EqualTo("Astronomy"));
    }

    [Test]
    public async Task Teacher_list_includes_question_stats()
    {
        using var teacher = await SignupAsync("teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await teacher.GetAsync("/api/assessments");
        var item = response.Body.GetProperty("assessments").EnumerateArray()
            .First(a => a.GetProperty("id").GetGuid() == assessmentId);

        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(item.GetProperty("questionCount").GetInt32(), Is.EqualTo(2));
        Assert.That(item.GetProperty("submissionCount").GetInt32(), Is.EqualTo(0));
        Assert.That(item.GetProperty("avgScore").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Delete_removes_assessment()
    {
        using var teacher = await SignupAsync("teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var delete = await teacher.DeleteAsync($"/api/assessments/{assessmentId}");
        var get = await teacher.GetAsync($"/api/assessments/{assessmentId}");

        Assert.That(delete.StatusCode, Is.EqualTo(200));
        Assert.That(get.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Non_owner_cannot_delete_assessment()
    {
        using var owner = await SignupAsync("teacher");
        using var other = await SignupAsync("teacher");
        var assessmentId = await CreateAssessmentAsync(owner);

        var delete = await other.DeleteAsync($"/api/assessments/{assessmentId}");
        var get = await owner.GetAsync($"/api/assessments/{assessmentId}");

        Assert.That(delete.StatusCode, Is.EqualTo(404));
        Assert.That(get.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task Create_rejects_short_title_and_empty_questions()
    {
        using var teacher = await SignupAsync("teacher");

        var shortTitle = await teacher.CreateAssessmentAsync(
            TestData.AssessmentDraft(title: "No"));
        var noQuestions = await teacher.CreateAssessmentAsync(new
        {
            title = TestData.UniqueTitle(),
            durationMinutes = 10,
            questions = Array.Empty<object>(),
        });

        Assert.That(shortTitle.StatusCode, Is.EqualTo(400));
        Assert.That(
            shortTitle.Error,
            Is.EqualTo("Give the assessment a title (at least 4 characters)."));
        Assert.That(noQuestions.StatusCode, Is.EqualTo(400));
        Assert.That(noQuestions.Error, Is.EqualTo("Add at least one question."));
    }

    [Test]
    public async Task Health_endpoint_reports_ok()
    {
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.GetAsync("/api/health");

        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Body.GetProperty("ok").GetBoolean(), Is.True);
    }
}
