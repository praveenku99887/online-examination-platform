using System.Data.Common;
using System.Text.Json;
using Ascend.Api.Data;
using Dapper;

namespace Ascend.Api.Services;

public static class DemoSeed
{
    private sealed record DemoCredential(string Email, string Name, string Password);

    private static readonly Dictionary<string, DemoCredential> DemoCredentials = new()
    {
        ["teacher"] = new("teacher@demo.academy", "Ada Lovelace", "demo-teacher-2026"),
        ["student"] = new("student@demo.academy", "Alan Turing", "demo-student-2026"),
    };

    private sealed record SampleOption(string Text, bool Correct, string WhyWrong);

    private sealed record SampleQuestion(string Text, string Topic, string Explanation, SampleOption[] Options);

    private sealed record SampleAssessment(string Title, string Description, string Subject, int DurationMinutes, SampleQuestion[] Questions);

    private static readonly SampleAssessment Sample = new(
        "Foundations of Modern Physics",
        "A curated checkpoint across relativity, quantum mechanics and thermodynamics — designed to test conceptual clarity, not memorisation.",
        "Physics",
        12,
        new[]
        {
            new SampleQuestion(
                "According to special relativity, what happens to time for an object moving close to the speed of light relative to a stationary observer?",
                "Relativity",
                "Time dilation is a direct consequence of the constancy of the speed of light: moving clocks tick slower relative to a stationary frame, as described by the Lorentz factor γ = 1/√(1−v²/c²).",
                new[]
                {
                    new SampleOption("Time runs slower for the moving object (time dilation)", true, ""),
                    new SampleOption("Time runs faster for the moving object", false, "This inverts the effect — the Lorentz factor γ ≥ 1 always makes moving intervals longer, never shorter."),
                    new SampleOption("Time is unaffected because it is absolute", false, "Newton assumed absolute time, but Einstein showed simultaneity is relative — absolute time contradicts the observed constancy of c."),
                    new SampleOption("Time stops completely at any speed", false, "Time only approaches a stop asymptotically as v → c; at ordinary speeds dilation is negligible but non-zero."),
                }),
            new SampleQuestion(
                "In the double-slit experiment, what happens when particles are observed at the slits?",
                "Quantum Mechanics",
                "Measurement collapses the wavefunction: interference disappears and particles behave like classical particles, producing two bands instead of an interference pattern.",
                new[]
                {
                    new SampleOption("The interference pattern disappears", true, ""),
                    new SampleOption("The interference pattern becomes stronger", false, "Observation destroys the superposition that creates interference, so the pattern can never be enhanced by measurement."),
                    new SampleOption("Particles split into two halves, one through each slit", false, "Particles are never detected as halves — detectors always register whole quanta; the wave nature describes probability, not physical splitting."),
                    new SampleOption("Nothing changes; observation has no effect", false, "This is the classical intuition the experiment refutes — which-path information fundamentally alters the outcome."),
                }),
            new SampleQuestion(
                "Which statement best captures the Second Law of Thermodynamics?",
                "Thermodynamics",
                "In an isolated system, entropy never decreases — processes are spontaneous only when they increase or conserve total entropy.",
                new[]
                {
                    new SampleOption("Entropy of an isolated system never decreases", true, ""),
                    new SampleOption("Energy can be created but never destroyed", false, "This violates the First Law (conservation of energy) — energy can neither be created nor destroyed."),
                    new SampleOption("Heat naturally flows from cold to hot bodies", false, "Heat spontaneously flows hot → cold; reversing that flow requires external work, exactly as the Second Law demands."),
                    new SampleOption("Entropy always decreases in closed systems", false, "This is the precise opposite of the law; decreasing-entropy claims define a perpetual motion machine of the second kind."),
                }),
            new SampleQuestion(
                "What does E = mc² fundamentally express?",
                "Relativity",
                "Mass–energy equivalence: mass is a concentrated form of energy, and a small amount of mass corresponds to an enormous amount of energy via the factor c².",
                new[]
                {
                    new SampleOption("Mass and energy are interchangeable forms of the same quantity", true, ""),
                    new SampleOption("Energy travels at twice the speed of light", false, "c² is a conversion factor between units, not a velocity — nothing in the equation suggests superluminal travel."),
                    new SampleOption("Mass can only be converted to energy at light speed", false, "Conversion happens in any nuclear or annihilation process; v never appears in the equation at all."),
                    new SampleOption("Energy has mass only inside stars", false, "The relation is universal — it applies to a battery, a spring, or a star equally; binding energy changes mass everywhere."),
                }),
            new SampleQuestion(
                "The photoelectric effect demonstrates that light…",
                "Quantum Mechanics",
                "Einstein showed light arrives in quanta (photons); electron emission depends on frequency, not intensity — proving particle-like energy packets E = hf.",
                new[]
                {
                    new SampleOption("is quantised into packets of energy called photons", true, ""),
                    new SampleOption("behaves purely as a continuous wave", false, "A pure wave cannot explain the sharp frequency threshold — below it, no electrons are emitted regardless of intensity."),
                    new SampleOption("travels slower than electrons", false, "Speed comparisons are irrelevant here; the effect concerns energy transfer, and photons always travel at c in vacuum."),
                    new SampleOption("has infinite energy at high frequency", false, "Photon energy grows linearly with frequency (E = hf), finitely — nothing diverges."),
                }),
            new SampleQuestion(
                "An ideal gas is compressed adiabatically. What happens to its temperature?",
                "Thermodynamics",
                "With no heat exchange (Q = 0), the work done on the gas becomes internal energy, so temperature rises — described by TV^(γ−1) = constant.",
                new[]
                {
                    new SampleOption("It increases", true, ""),
                    new SampleOption("It decreases", false, "Cooling occurs on adiabatic expansion, when the gas does work on its surroundings — compression is the reverse."),
                    new SampleOption("It stays constant because no heat is exchanged", false, "Constant temperature defines an isothermal process; adiabatic means Q = 0, not ΔT = 0 — work still changes internal energy."),
                    new SampleOption("It first increases then returns to its initial value", false, "Without heat flow there is no mechanism to shed the added internal energy, so no return to the initial temperature."),
                }),
            new SampleQuestion(
                "Heisenberg's uncertainty principle states that…",
                "Quantum Mechanics",
                "Δx·Δp ≥ ħ/2: position and momentum cannot both be known to arbitrary precision simultaneously — a fundamental property of quantum states, not a measurement flaw.",
                new[]
                {
                    new SampleOption("position and momentum cannot both be precisely known at once", true, ""),
                    new SampleOption("electrons cannot be observed without destroying them", false, "The principle is not about measurement damage — it is a mathematical property of wavefunctions and non-commuting operators."),
                    new SampleOption("energy is always uncertain by a fixed amount", false, "The energy–time relation is a separate inequality with a different meaning; energy itself is not 'always uncertain'."),
                    new SampleOption("all measurements are completely random", false, "Quantum mechanics gives precise probability distributions — outcomes are probabilistic, not lawless."),
                }),
            new SampleQuestion(
                "Which phenomenon provides direct evidence that the universe is expanding?",
                "Cosmology",
                "Hubble's observation of redshifted galactic spectra — light stretched to longer wavelengths by receding galaxies — scales with distance, evidencing expansion.",
                new[]
                {
                    new SampleOption("Redshift of light from distant galaxies", true, ""),
                    new SampleOption("Blueshift of nearby star clusters", false, "Blueshift means approaching motion — if universal, it would suggest contraction, the opposite of expansion."),
                    new SampleOption("The constant brightness of the night sky", false, "A dark night sky (Olbers' paradox) is actually a consequence of expansion and finite light-travel time — constant brightness would argue for a static infinite universe."),
                    new SampleOption("Gravity waves from black hole mergers", false, "Gravitational waves confirm relativity's predictions but carry no direct measurement of cosmic expansion."),
                }),
        });

    public static async Task<SessionUser> GetOrCreateDemoUserAsync(string role)
    {
        await using var conn = await Db.OpenConnectionAsync();
        var teacher = await EnsureUserAsync(conn, "teacher");
        var sampleId = await SeedTeacherSampleAsync(conn, teacher.Id);
        var student = await EnsureUserAsync(conn, "student");
        await SeedStudentHistoryAsync(conn, student.Id, sampleId);
        return role == "teacher" ? teacher : student;
    }

    private static async Task<SessionUser> EnsureUserAsync(DbConnection conn, string role)
    {
        var cred = DemoCredentials[role];
        var existing = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT * FROM users WHERE email = @email",
            new { email = cred.Email });
        if (existing != null)
            return new SessionUser(existing.Id, existing.Name, existing.Email, existing.Role);

        var createdId = Db.NewId();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, name, email, password_hash, role, created_at) VALUES (@id, @name, @email, @passwordHash, @role, @now)",
            new { id = createdId, name = cred.Name, email = cred.Email, passwordHash = Passwords.Hash(cred.Password), role, now = Db.NowDb() });
        var created = await conn.QuerySingleAsync<UserRow>(
            "SELECT * FROM users WHERE id = @id",
            new { id = createdId });
        return new SessionUser(created.Id, created.Name, created.Email, created.Role);
    }

    private static async Task<Guid?> SeedTeacherSampleAsync(DbConnection conn, Guid teacherId)
    {
        var found = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT id FROM assessments WHERE teacher_id = @teacherId",
            new { teacherId = teacherId.ToString() });
        if (found != null) return found;
        return await CreateSampleAssessmentAsync(conn, teacherId);
    }

    private static async Task SeedStudentHistoryAsync(DbConnection conn, Guid studentId, Guid? sampleAssessmentId)
    {
        if (sampleAssessmentId == null) return;
        var existing = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT id FROM attempts WHERE student_id = @studentId",
            new { studentId = studentId.ToString() });
        if (existing != null) return;

        var qs = (await conn.QueryAsync<QuestionRow>(
            "SELECT * FROM questions WHERE assessment_id = @aid",
            new { aid = sampleAssessmentId!.Value.ToString() })).OrderBy(q => q.Idx).ToList();

        static bool PickWrong(int qIdx) => qIdx == 2 || qIdx == 5;

        var answers = new Dictionary<string, string>();
        var timings = new Dictionary<string, int>();
        var correct = 0;
        var timeArray = new[] { 38, 52, 44, 61, 47, 71, 55, 42 };

        for (var i = 0; i < qs.Count; i++)
        {
            var opts = (await conn.QueryAsync<OptionRow>(
                "SELECT * FROM options WHERE question_id = @qid",
                new { qid = qs[i].Id.ToString() })).OrderBy(o => o.Idx).ToList();
            var right = opts.First(o => o.IsCorrect);
            var chosen = PickWrong(i) ? opts.First(o => !o.IsCorrect) : right;
            if (chosen.IsCorrect) correct++;
            answers[qs[i].Id.ToString()] = chosen.Id.ToString();
            timings[qs[i].Id.ToString()] = timeArray[i % 8];
        }

        var timeTaken = timings.Values.Sum();
        var started = Clock.UtcNowMs().AddDays(-2);

        await conn.ExecuteAsync(
            "INSERT INTO attempts (id, assessment_id, student_id, status, started_at, expires_at, submitted_at, answers, timings, correct_count, total_questions, time_taken_seconds) " +
            "VALUES (@id, @assessmentId, @studentId, 'submitted', @startedAt, @expiresAt, @submittedAt, @answers, @timings, @correctCount, @totalQuestions, @timeTakenSeconds)",
            new
            {
                id = Db.NewId(),
                assessmentId = sampleAssessmentId!.Value.ToString(),
                studentId = studentId.ToString(),
                startedAt = Db.ToDb(started),
                expiresAt = Db.ToDb(started.AddMinutes(Sample.DurationMinutes)),
                submittedAt = Db.ToDb(started.AddSeconds(timeTaken)),
                answers = JsonSerializer.Serialize(answers),
                timings = JsonSerializer.Serialize(timings),
                correctCount = correct,
                totalQuestions = qs.Count,
                timeTakenSeconds = timeTaken,
            });
    }

    private static async Task<Guid> CreateSampleAssessmentAsync(DbConnection conn, Guid teacherId)
    {
        var assessmentId = Db.NewId();
        await conn.ExecuteAsync(
            "INSERT INTO assessments (id, teacher_id, title, description, subject, duration_minutes, published, is_ai_generated, created_at) " +
            "VALUES (@id, @teacherId, @title, @description, @subject, @durationMinutes, 1, 1, @now)",
            new
            {
                id = assessmentId,
                teacherId = teacherId.ToString(),
                title = Sample.Title,
                description = Sample.Description,
                subject = Sample.Subject,
                durationMinutes = Sample.DurationMinutes,
                now = Db.NowDb(),
            });
        var a = await conn.QuerySingleAsync<AssessmentRow>(
            "SELECT * FROM assessments WHERE id = @id", new { id = assessmentId });

        for (var i = 0; i < Sample.Questions.Length; i++)
        {
            var q = Sample.Questions[i];
            var questionId = Db.NewId();
            await conn.ExecuteAsync(
                "INSERT INTO questions (id, assessment_id, idx, text, topic, correct_explanation, created_at) VALUES (@id, @assessmentId, @idx, @text, @topic, @correctExplanation, @now)",
                new
                {
                    id = questionId,
                    assessmentId = a.Id.ToString(),
                    idx = i,
                    text = q.Text,
                    topic = q.Topic,
                    correctExplanation = q.Explanation,
                    now = Db.NowDb(),
                });
            var qRow = await conn.QuerySingleAsync<QuestionRow>(
                "SELECT * FROM questions WHERE id = @id", new { id = questionId });

            for (var j = 0; j < q.Options.Length; j++)
            {
                var o = q.Options[j];
                await conn.ExecuteAsync(
                    "INSERT INTO options (id, question_id, idx, text, is_correct, wrong_explanation) VALUES (@id, @questionId, @idx, @text, @isCorrect, @wrongExplanation)",
                    new
                    {
                        id = Db.NewId(),
                        questionId = qRow.Id.ToString(),
                        idx = j,
                        text = o.Text,
                        isCorrect = o.Correct,
                        wrongExplanation = o.WhyWrong,
                    });
            }
        }

        return a.Id;
    }
}
