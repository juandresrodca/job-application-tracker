using Microsoft.Data.Sqlite;

namespace JobTracker.Infrastructure.Data;

/// <summary>
/// Manages SQLite connection and schema initialization.
/// Uses Dapper for simple, performant data access without EF overhead.
/// </summary>
public class DatabaseContext
{
    private readonly string _connectionString;

    public DatabaseContext(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // Enable WAL mode for better concurrency
        await ExecuteAsync(conn, "PRAGMA journal_mode=WAL;");
        await ExecuteAsync(conn, "PRAGMA foreign_keys=ON;");

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Companies (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL UNIQUE,
                Website     TEXT,
                Industry    TEXT,
                Location    TEXT,
                Notes       TEXT
            );
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Contacts (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL,
                Email       TEXT,
                Phone       TEXT,
                LinkedInUrl TEXT,
                Role        TEXT,
                Notes       TEXT,
                CompanyId   INTEGER REFERENCES Companies(Id) ON DELETE SET NULL
            );
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Skills (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL UNIQUE,
                Category    TEXT
            );
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS JobApplications (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                RoleName        TEXT NOT NULL,
                JobDescription  TEXT,
                Status          INTEGER NOT NULL DEFAULT 0,
                AppliedDate     TEXT NOT NULL,
                LastUpdated     TEXT,
                JobPostingUrl   TEXT,
                SalaryRange     TEXT,
                IsRemote        INTEGER NOT NULL DEFAULT 0,
                Notes           TEXT,
                CompanyId       INTEGER NOT NULL REFERENCES Companies(Id) ON DELETE CASCADE,
                ContactId       INTEGER REFERENCES Contacts(Id) ON DELETE SET NULL
            );
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS ApplicationSkills (
                JobApplicationId INTEGER NOT NULL REFERENCES JobApplications(Id) ON DELETE CASCADE,
                SkillId          INTEGER NOT NULL REFERENCES Skills(Id) ON DELETE CASCADE,
                IsOwned          INTEGER NOT NULL DEFAULT 0,
                IsRequired       INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (JobApplicationId, SkillId)
            );
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Interviews (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                JobApplicationId INTEGER NOT NULL REFERENCES JobApplications(Id) ON DELETE CASCADE,
                ScheduledAt     TEXT NOT NULL,
                DurationMinutes INTEGER NOT NULL DEFAULT 60,
                Type            INTEGER NOT NULL DEFAULT 0,
                Interviewer     TEXT,
                LocationOrLink  TEXT,
                Notes           TEXT,
                IsCompleted     INTEGER NOT NULL DEFAULT 0
            );
            """);

        await ExecuteAsync(conn, """
            CREATE INDEX IF NOT EXISTS idx_applications_date ON JobApplications(AppliedDate);
            CREATE INDEX IF NOT EXISTS idx_applications_status ON JobApplications(Status);
            CREATE INDEX IF NOT EXISTS idx_applications_company ON JobApplications(CompanyId);
            CREATE INDEX IF NOT EXISTS idx_interviews_date ON Interviews(ScheduledAt);
            CREATE INDEX IF NOT EXISTS idx_interviews_application ON Interviews(JobApplicationId);
            """);
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
