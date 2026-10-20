using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Ascend.Api.Data;

/// <summary>
/// SQLite connection factory + schema bootstrap. The database is a single
/// file (ascend.db by default); no server install needed.
/// Conventions: Guid stored as TEXT (36-char), DateTime as TEXT (ISO 8601 UTC),
/// bool as INTEGER 0/1, JSON columns as TEXT.
/// </summary>
public static class Db
{
    private static string _connectionString = "Data Source=ascend.db";
    private static bool _useSqlServer;

    public static void Configure(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Data Source=ascend.db";

        // SQL Server connection strings carry "Server=" (e.g. "Server=tcp:host,1433;...");
        // anything else is treated as a SQLite file path/connection string.
        _useSqlServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);

        if (!_useSqlServer && !connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
            connectionString = $"Data Source={connectionString}";

        _connectionString = connectionString;
    }

    public static async Task<DbConnection> OpenConnectionAsync()
    {
        DbConnection conn = _useSqlServer
            ? new SqlConnection(_connectionString)
            : new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        if (!_useSqlServer)
        {
            await using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            await pragma.ExecuteNonQueryAsync();
        }
        return conn;
    }

    private const string SchemaA = """
        CREATE TABLE IF NOT EXISTS users (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          email TEXT NOT NULL UNIQUE,
          password_hash TEXT NOT NULL,
          role TEXT NOT NULL,
          created_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS sessions (
          id TEXT PRIMARY KEY,
          user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
          token TEXT NOT NULL UNIQUE,
          expires_at TEXT NOT NULL,
          created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS sessions_user_idx ON sessions (user_id);
        CREATE TABLE IF NOT EXISTS assessments (
          id TEXT PRIMARY KEY,
          teacher_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
          title TEXT NOT NULL,
          description TEXT NOT NULL DEFAULT '',
          subject TEXT NOT NULL DEFAULT 'General',
          duration_minutes INTEGER NOT NULL DEFAULT 30,
          published INTEGER NOT NULL DEFAULT 1,
          is_ai_generated INTEGER NOT NULL DEFAULT 0,
          created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS assessments_teacher_idx ON assessments (teacher_id);
        CREATE TABLE IF NOT EXISTS questions (
          id TEXT PRIMARY KEY,
          assessment_id TEXT NOT NULL REFERENCES assessments(id) ON DELETE CASCADE,
          idx INTEGER NOT NULL DEFAULT 0,
          text TEXT NOT NULL,
          topic TEXT NOT NULL DEFAULT 'General',
          correct_explanation TEXT NOT NULL DEFAULT '',
          created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS questions_assessment_idx ON questions (assessment_id);
        """;

    private const string SchemaB = """
        CREATE TABLE IF NOT EXISTS options (
          id TEXT PRIMARY KEY,
          question_id TEXT NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
          idx INTEGER NOT NULL DEFAULT 0,
          text TEXT NOT NULL,
          is_correct INTEGER NOT NULL DEFAULT 0,
          wrong_explanation TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS options_question_idx ON options (question_id);
        CREATE TABLE IF NOT EXISTS attempts (
          id TEXT PRIMARY KEY,
          assessment_id TEXT NOT NULL REFERENCES assessments(id) ON DELETE CASCADE,
          student_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
          status TEXT NOT NULL DEFAULT 'in_progress',
          started_at TEXT NOT NULL,
          expires_at TEXT NOT NULL,
          submitted_at TEXT,
          answers TEXT NOT NULL DEFAULT '{}',
          timings TEXT NOT NULL DEFAULT '{}',
          correct_count INTEGER NOT NULL DEFAULT 0,
          total_questions INTEGER NOT NULL DEFAULT 0,
          time_taken_seconds INTEGER NOT NULL DEFAULT 0,
          auto_submitted INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS attempts_assessment_idx ON attempts (assessment_id);
        CREATE INDEX IF NOT EXISTS attempts_student_idx ON attempts (student_id);
        CREATE TABLE IF NOT EXISTS ai_providers (
          id TEXT PRIMARY KEY,
          user_id TEXT NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
          base_url TEXT NOT NULL,
          api_key TEXT NOT NULL,
          model TEXT NOT NULL DEFAULT 'gpt-4o-mini',
          created_at TEXT NOT NULL,
          updated_at TEXT NOT NULL
        );
        """;

    // Same logical schema as above, expressed for SQL Server (Azure SQL). Columns
    // keep the TEXT-storage convention (Guid/DateTime as strings) so the Dapper
    // type handlers and all queries behave identically on both providers.
    // Note: attempts.student_id has no ON DELETE CASCADE because SQL Server
    // rejects the resulting multiple cascade path (users -> assessments -> attempts
    // and users -> attempts); the app never deletes users.
    private const string SqlServerSchema = """
        IF OBJECT_ID(N'users', N'U') IS NULL CREATE TABLE users (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          name nvarchar(200) NOT NULL,
          email nvarchar(320) NOT NULL UNIQUE,
          password_hash nvarchar(100) NOT NULL,
          role nvarchar(50) NOT NULL,
          created_at nvarchar(30) NOT NULL);
        IF OBJECT_ID(N'sessions', N'U') IS NULL CREATE TABLE sessions (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          user_id nvarchar(36) NOT NULL,
          token nvarchar(64) NOT NULL UNIQUE,
          expires_at nvarchar(30) NOT NULL,
          created_at nvarchar(30) NOT NULL,
          CONSTRAINT fk_sessions_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'sessions_user_idx' AND object_id = OBJECT_ID(N'sessions')) CREATE INDEX sessions_user_idx ON sessions (user_id);
        IF OBJECT_ID(N'assessments', N'U') IS NULL CREATE TABLE assessments (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          teacher_id nvarchar(36) NOT NULL,
          title nvarchar(300) NOT NULL,
          description nvarchar(max) NOT NULL DEFAULT N'',
          subject nvarchar(200) NOT NULL DEFAULT N'General',
          duration_minutes int NOT NULL DEFAULT 30,
          published bit NOT NULL DEFAULT 1,
          is_ai_generated bit NOT NULL DEFAULT 0,
          created_at nvarchar(30) NOT NULL,
          CONSTRAINT fk_assessments_teacher FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'assessments_teacher_idx' AND object_id = OBJECT_ID(N'assessments')) CREATE INDEX assessments_teacher_idx ON assessments (teacher_id);
        IF OBJECT_ID(N'questions', N'U') IS NULL CREATE TABLE questions (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          assessment_id nvarchar(36) NOT NULL,
          idx int NOT NULL DEFAULT 0,
          text nvarchar(max) NOT NULL,
          topic nvarchar(200) NOT NULL DEFAULT N'General',
          correct_explanation nvarchar(max) NOT NULL DEFAULT N'',
          created_at nvarchar(30) NOT NULL,
          CONSTRAINT fk_questions_assessment FOREIGN KEY (assessment_id) REFERENCES assessments(id) ON DELETE CASCADE);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'questions_assessment_idx' AND object_id = OBJECT_ID(N'questions')) CREATE INDEX questions_assessment_idx ON questions (assessment_id);
        IF OBJECT_ID(N'options', N'U') IS NULL CREATE TABLE options (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          question_id nvarchar(36) NOT NULL,
          idx int NOT NULL DEFAULT 0,
          text nvarchar(max) NOT NULL,
          is_correct bit NOT NULL DEFAULT 0,
          wrong_explanation nvarchar(max) NOT NULL DEFAULT N'',
          CONSTRAINT fk_options_question FOREIGN KEY (question_id) REFERENCES questions(id) ON DELETE CASCADE);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'options_question_idx' AND object_id = OBJECT_ID(N'options')) CREATE INDEX options_question_idx ON options (question_id);
        IF OBJECT_ID(N'attempts', N'U') IS NULL CREATE TABLE attempts (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          assessment_id nvarchar(36) NOT NULL,
          student_id nvarchar(36) NOT NULL,
          status nvarchar(50) NOT NULL DEFAULT N'in_progress',
          started_at nvarchar(30) NOT NULL,
          expires_at nvarchar(30) NOT NULL,
          submitted_at nvarchar(30) NULL,
          answers nvarchar(max) NOT NULL DEFAULT N'{}',
          timings nvarchar(max) NOT NULL DEFAULT N'{}',
          correct_count int NOT NULL DEFAULT 0,
          total_questions int NOT NULL DEFAULT 0,
          time_taken_seconds int NOT NULL DEFAULT 0,
          auto_submitted bit NOT NULL DEFAULT 0,
          CONSTRAINT fk_attempts_assessment FOREIGN KEY (assessment_id) REFERENCES assessments(id) ON DELETE CASCADE,
          CONSTRAINT fk_attempts_student FOREIGN KEY (student_id) REFERENCES users(id));
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'attempts_assessment_idx' AND object_id = OBJECT_ID(N'attempts')) CREATE INDEX attempts_assessment_idx ON attempts (assessment_id);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'attempts_student_idx' AND object_id = OBJECT_ID(N'attempts')) CREATE INDEX attempts_student_idx ON attempts (student_id);
        IF OBJECT_ID(N'ai_providers', N'U') IS NULL CREATE TABLE ai_providers (
          id nvarchar(36) NOT NULL PRIMARY KEY,
          user_id nvarchar(36) NOT NULL UNIQUE,
          base_url nvarchar(500) NOT NULL,
          api_key nvarchar(500) NOT NULL,
          model nvarchar(200) NOT NULL DEFAULT N'gpt-4o-mini',
          created_at nvarchar(30) NOT NULL,
          updated_at nvarchar(30) NOT NULL,
          CONSTRAINT fk_ai_providers_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE);
        """;

    public static async Task InitAsync()
    {
        SqlMapper.AddTypeHandler(new GuidTextHandler());
        SqlMapper.AddTypeHandler(new DateTimeTextHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeTextHandler());
        SqlMapper.AddTypeHandler(new NullableGuidTextHandler());

        await using var conn = await OpenConnectionAsync();
        var schemas = _useSqlServer
            ? new[] { SqlServerSchema }
            : new[] { SchemaA, SchemaB };
        foreach (var schema in schemas)
        {
            var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var stmt in statements)
                await conn.ExecuteAsync(stmt);
        }
    }

    private sealed class GuidTextHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString();
        }
        public override Guid Parse(object value) => Guid.Parse(value.ToString()!);
    }

    private sealed class DateTimeTextHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = ToDb(value);
        }
        public override DateTime Parse(object value) =>
            DateTime.Parse(value.ToString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
    }

    private sealed class NullableDateTimeTextHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue ? (object)ToDb(value.Value) : DBNull.Value;
        }
        public override DateTime? Parse(object value) =>
            value is null || value is DBNull ? null
            : DateTime.Parse(value.ToString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
    }

    private sealed class NullableGuidTextHandler : SqlMapper.TypeHandler<Guid?>
    {
        public override void SetValue(IDbDataParameter parameter, Guid? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue ? (object)value.Value.ToString() : DBNull.Value;
        }
        public override Guid? Parse(object value) =>
            value is null || value is DBNull ? null : Guid.Parse(value.ToString()!);
    }

    public static string NewId() => Guid.NewGuid().ToString();

    public static string ToDb(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    public static string NowDb() => ToDb(DateTime.UtcNow);
}
