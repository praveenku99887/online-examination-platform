using System.Text.Json;
using Ascend.Api.Tests.Shared;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

[Collection(ApiCollection.Name)]
public sealed class AssessmentTests
{
    private readonly AscendWebApplicationFactory _factory;

    public AssessmentTests(AscendWebApplicationFactory factory) => _factory = factory;

    private static async Task<ApiClient> SignupAsync(
        AscendWebApplicationFactory factory,
        string role)
    {
        var api = new ApiClient(factory);
        var name = role == "teacher" ? "Test Teacher" : "Test Student";
        var response = await api.SignupAsync(name, TestData.UniqueEmail(role), role: role);
        Assert.Equal(200, response.StatusCode);
        return api;
    }

    private static async Task<Guid> CreateAssessmentAsync(
        ApiClient teacher,
        object? draft = null)
    {
        var response = await teacher.CreateAssessmentAsync(draft ?? TestData.AssessmentDraft());
        Assert.Equal(200, response.StatusCode);
        return response.Body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_rejects_short_title()
    {
        using var teacher = await SignupAsync(_factory, "teacher");

        var response = await teacher.CreateAssessmentAsync(
            TestData.AssessmentDraft(title: "No"));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "Give the assessment a title (at least 4 characters).", response.Error);
    }

    [Fact]
    public async Task Create_rejects_empty_question_list()
    {
        using var teacher = await SignupAsync(_factory, "teacher");

        var response = await teacher.CreateAssessmentAsync(new
        {
            title = TestData.UniqueTitle(),
            durationMinutes = 10,
            questions = Array.Empty<object>(),
        });

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Add at least one question.", response.Error);
    }

    [Fact]
    public async Task Create_rejects_duration_outside_range()
    {
        using var teacher = await SignupAsync(_factory, "teacher");

        var response = await teacher.CreateAssessmentAsync(
            TestData.AssessmentDraft(durationMinutes: 0));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Duration must be between 1 and 300 minutes.", response.Error);
    }

    [Fact]
    public async Task Create_rejects_question_with_one_option()
    {
        using var teacher = await SignupAsync(_factory, "teacher");

        var response = await teacher.CreateAssessmentAsync(new
        {
            title = TestData.UniqueTitle(),
            durationMinutes = 10,
            questions = new[]
            {
                new
                {
                    text = "One option only here",
                    options = new[]
                    {
                        new { text = "Solo", correct = true, whyWrong = "" },
                    },
                },
            },
        });

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("2", response.Error);
    }

    [Fact]
    public async Task Create_rejects_multiple_correct_options()
    {
        using var teacher = await SignupAsync(_factory, "teacher");

        var response = await teacher.CreateAssessmentAsync(new
        {
            title = TestData.UniqueTitle(),
            durationMinutes = 10,
            questions = new[]
            {
                new
                {
                    text = "Two correct options here",
                    options = new[]
                    {
                        new { text = "A", correct = true, whyWrong = "" },
                        new { text = "B", correct = true, whyWrong = "" },
                    },
                },
            },
        });

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("exactly one correct", response.Error);
    }

    [Fact]
    public async Task Teacher_can_read_own_assessment_with_topics()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await teacher.GetAsync($"/api/assessments/{assessmentId}");

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body.GetProperty("isOwner").GetBoolean());

        var assessment = response.Body.GetProperty("assessment");
        Assert.Equal(2, assessment.GetProperty("questions").GetArrayLength());
        Assert.Equal(
            "Astronomy",
            assessment.GetProperty("questions")[0].GetProperty("topic").GetString());
    }

    [Fact]
    public async Task Student_get_hides_topics_and_answer_key()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await student.GetAsync($"/api/assessments/{assessmentId}");

        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Body.GetProperty("isOwner").GetBoolean());

        foreach (var question in response.Body
                     .GetProperty("assessment")
                     .GetProperty("questions")
                     .EnumerateArray())
        {
            Assert.False(question.TryGetProperty("topic", out _));
            Assert.False(question.TryGetProperty("correctExplanation", out _));

            foreach (var option in question.GetProperty("options").EnumerateArray())
            {
                Assert.False(option.TryGetProperty("isCorrect", out _));
                Assert.False(option.TryGetProperty("wrongExplanation", out _));
            }
        }
    }

    [Fact]
    public async Task Student_list_returns_published_assessments()
    {
        // Regression test for the Azure SQL failure: the student branch used
        // "WHERE a.published", which is invalid for SQL Server BIT columns.
        using var teacher = await SignupAsync(_factory, "teacher");
        using var student = await SignupAsync(_factory, "student");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await student.GetAsync("/api/assessments");

        Assert.Equal(200, response.StatusCode);
        var assessments = response.Body.GetProperty("assessments").EnumerateArray().ToArray();
        var item = assessments.FirstOrDefault(
            a => a.GetProperty("id").GetGuid() == assessmentId);

        Assert.NotEqual(JsonValueKind.Undefined, item.ValueKind);
        Assert.Equal(2, item.GetProperty("questionCount").GetInt32());
        Assert.Equal(0, item.GetProperty("attemptCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("bestScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("inProgressAttemptId").ValueKind);
    }

    [Fact]
    public async Task Teacher_list_includes_question_and_submission_stats()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var response = await teacher.GetAsync("/api/assessments");

        Assert.Equal(200, response.StatusCode);
        var item = response.Body.GetProperty("assessments").EnumerateArray()
            .First(a => a.GetProperty("id").GetGuid() == assessmentId);

        Assert.Equal(2, item.GetProperty("questionCount").GetInt32());
        Assert.Equal(0, item.GetProperty("submissionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("avgScore").ValueKind);
    }

    [Fact]
    public async Task Delete_removes_assessment()
    {
        using var teacher = await SignupAsync(_factory, "teacher");
        var assessmentId = await CreateAssessmentAsync(teacher);

        var delete = await teacher.DeleteAsync($"/api/assessments/{assessmentId}");
        Assert.Equal(200, delete.StatusCode);

        var get = await teacher.GetAsync($"/api/assessments/{assessmentId}");
        Assert.Equal(404, get.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_delete_assessment()
    {
        using var owner = await SignupAsync(_factory, "teacher");
        using var other = await SignupAsync(_factory, "teacher");
        var assessmentId = await CreateAssessmentAsync(owner);

        var delete = await other.DeleteAsync($"/api/assessments/{assessmentId}");
        Assert.Equal(404, delete.StatusCode);

        var get = await owner.GetAsync($"/api/assessments/{assessmentId}");
        Assert.Equal(200, get.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_ok()
    {
        using var api = new ApiClient(_factory);

        var response = await api.GetAsync("/api/health");

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body.GetProperty("ok").GetBoolean());
    }
}
