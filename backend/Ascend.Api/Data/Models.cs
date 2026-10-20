namespace Ascend.Api.Data;

public class UserRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class AssessmentRow
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Subject { get; set; } = "";
    public int DurationMinutes { get; set; }
    public bool Published { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssessmentWithTeacherRow : AssessmentRow
{
    public string TeacherName { get; set; } = "";
}

public class StudentAssessmentRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Subject { get; set; } = "";
    public int DurationMinutes { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TeacherName { get; set; } = "";
}

public class QuestionRow
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int Idx { get; set; }
    public string Text { get; set; } = "";
    public string Topic { get; set; } = "";
    public string CorrectExplanation { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class OptionRow
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Idx { get; set; }
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string WrongExplanation { get; set; } = "";
}

public class AttemptRow
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string Answers { get; set; } = "{}";
    public string Timings { get; set; } = "{}";
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public int TimeTakenSeconds { get; set; }
    public bool AutoSubmitted { get; set; }
}

public class AttemptWithStudentRow : AttemptRow
{
    public string StudentName { get; set; } = "";
}

public class AiProviderRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record SessionUser(Guid Id, string Name, string Email, string Role);

public record ProviderCfg(string BaseUrl, string ApiKey, string Model);

public class DraftOption
{
    public string Text { get; set; } = "";
    public bool Correct { get; set; }
    public string WhyWrong { get; set; } = "";
}

public class DraftQuestion
{
    public string Text { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Explanation { get; set; } = "";
    public List<DraftOption> Options { get; set; } = new();
}

public class DraftAssessment
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Subject { get; set; } = "";
    public int DurationMinutes { get; set; }
    public List<DraftQuestion> Questions { get; set; } = new();
}
