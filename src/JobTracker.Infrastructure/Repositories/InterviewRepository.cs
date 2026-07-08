using Dapper;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Data;

namespace JobTracker.Infrastructure.Repositories;

public class InterviewRepository : IInterviewRepository
{
    private readonly DatabaseContext _db;

    public InterviewRepository(DatabaseContext db) => _db = db;

    // Columns joined with the owning application + company so calendar/dashboard
    // rows can render "Role at Company" without extra queries.
    private const string SelectWithApplication = """
        SELECT i.Id, i.JobApplicationId, i.ScheduledAt, i.DurationMinutes, i.Type,
               i.Interviewer, i.LocationOrLink, i.Notes, i.IsCompleted,
               ja.Id, ja.RoleName, ja.JobDescription, ja.Status, ja.AppliedDate, ja.LastUpdated,
               ja.JobPostingUrl, ja.SalaryRange, ja.IsRemote, ja.Notes, ja.CompanyId, ja.ContactId,
               c.Id, c.Name, c.Website, c.Industry, c.Location, c.Notes
        FROM Interviews i
        JOIN JobApplications ja ON ja.Id = i.JobApplicationId
        JOIN Companies c ON c.Id = ja.CompanyId
        """;

    private static Task<IEnumerable<Interview>> QueryWithApplicationAsync(
        System.Data.IDbConnection conn, string whereOrder, object? param = null)
    {
        return conn.QueryAsync<Interview, JobApplication, Company, Interview>(
            SelectWithApplication + "\n" + whereOrder,
            (i, ja, c) => { ja.Company = c; i.JobApplication = ja; return i; },
            param,
            splitOn: "Id,Id");
    }

    public async Task<Interview?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Interview>(
            "SELECT * FROM Interviews WHERE Id = @id", new { id });
    }

    public async Task<IEnumerable<Interview>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await QueryWithApplicationAsync(conn, "ORDER BY i.ScheduledAt");
    }

    public async Task<IEnumerable<Interview>> GetByApplicationAsync(int applicationId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Interview>(
            "SELECT * FROM Interviews WHERE JobApplicationId = @applicationId ORDER BY ScheduledAt",
            new { applicationId });
    }

    public async Task<IEnumerable<Interview>> GetUpcomingAsync(int days)
    {
        using var conn = _db.CreateConnection();
        return await QueryWithApplicationAsync(conn,
            """
            WHERE i.IsCompleted = 0
              AND datetime(i.ScheduledAt) >= datetime(@from)
              AND datetime(i.ScheduledAt) < datetime(@to)
            ORDER BY i.ScheduledAt
            """,
            new { from = DateTime.Now, to = DateTime.Today.AddDays(days + 1) });
    }

    public async Task<IEnumerable<Interview>> GetBetweenAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        using var conn = _db.CreateConnection();
        return await QueryWithApplicationAsync(conn,
            """
            WHERE datetime(i.ScheduledAt) >= datetime(@fromInclusive)
              AND datetime(i.ScheduledAt) < datetime(@toExclusive)
            ORDER BY i.ScheduledAt
            """,
            new { fromInclusive, toExclusive });
    }

    public async Task<Interview> AddAsync(Interview entity)
    {
        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO Interviews
                (JobApplicationId, ScheduledAt, DurationMinutes, Type, Interviewer, LocationOrLink, Notes, IsCompleted)
            VALUES
                (@JobApplicationId, @ScheduledAt, @DurationMinutes, @Type, @Interviewer, @LocationOrLink, @Notes, @IsCompleted);
            SELECT last_insert_rowid();
            """,
            entity);
        entity.Id = id;
        return entity;
    }

    public async Task UpdateAsync(Interview entity)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE Interviews SET
                ScheduledAt = @ScheduledAt, DurationMinutes = @DurationMinutes, Type = @Type,
                Interviewer = @Interviewer, LocationOrLink = @LocationOrLink,
                Notes = @Notes, IsCompleted = @IsCompleted
            WHERE Id = @Id
            """,
            entity);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Interviews WHERE Id = @id", new { id });
    }
}
