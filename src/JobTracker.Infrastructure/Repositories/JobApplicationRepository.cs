using Dapper;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Data;

namespace JobTracker.Infrastructure.Repositories;

public class JobApplicationRepository : IJobApplicationRepository
{
    private readonly DatabaseContext _db;

    public JobApplicationRepository(DatabaseContext db) => _db = db;

    public async Task<JobApplication?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<JobApplication>(
            "SELECT * FROM JobApplications WHERE Id = @id", new { id });
    }

    public async Task<JobApplication?> GetWithDetailsAsync(int id)
    {
        using var conn = _db.CreateConnection();

        var app = await conn.QueryFirstOrDefaultAsync<JobApplication>(
            "SELECT * FROM JobApplications WHERE Id = @id", new { id });

        if (app is null) return null;

        app.Company = await conn.QueryFirstOrDefaultAsync<Company>(
            "SELECT * FROM Companies WHERE Id = @id", new { id = app.CompanyId })
            ?? new Company();

        if (app.ContactId.HasValue)
            app.Contact = await conn.QueryFirstOrDefaultAsync<Contact>(
                "SELECT * FROM Contacts WHERE Id = @id", new { id = app.ContactId.Value });

        var skills = await conn.QueryAsync<ApplicationSkill, Skill, ApplicationSkill>(
            """
            SELECT aps.*, s.*
            FROM ApplicationSkills aps
            JOIN Skills s ON s.Id = aps.SkillId
            WHERE aps.JobApplicationId = @appId
            """,
            (aps, s) => { aps.Skill = s; return aps; },
            new { appId = id },
            splitOn: "Id");

        app.ApplicationSkills = skills.ToList();
        return app;
    }

    public async Task<IEnumerable<JobApplication>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();

        var sql = """
            SELECT ja.*, c.*, ct.*
            FROM JobApplications ja
            JOIN Companies c ON c.Id = ja.CompanyId
            LEFT JOIN Contacts ct ON ct.Id = ja.ContactId
            ORDER BY ja.AppliedDate DESC
            """;

        return await conn.QueryAsync<JobApplication, Company, Contact, JobApplication>(
            sql,
            (ja, c, ct) => { ja.Company = c; ja.Contact = ct; return ja; },
            splitOn: "Id,Id");
    }

    public async Task<IEnumerable<JobApplication>> GetByWeekAsync(int isoWeek, int year)
    {
        using var conn = _db.CreateConnection();

        // SQLite: use strftime to calculate ISO week
        var sql = """
            SELECT ja.*, c.*, ct.*
            FROM JobApplications ja
            JOIN Companies c ON c.Id = ja.CompanyId
            LEFT JOIN Contacts ct ON ct.Id = ja.ContactId
            WHERE CAST(strftime('%W', ja.AppliedDate) AS INTEGER) = @week
              AND CAST(strftime('%Y', ja.AppliedDate) AS INTEGER) = @year
            ORDER BY ja.AppliedDate DESC
            """;

        return await conn.QueryAsync<JobApplication, Company, Contact, JobApplication>(
            sql,
            (ja, c, ct) => { ja.Company = c; ja.Contact = ct; return ja; },
            new { week = isoWeek, year },
            splitOn: "Id,Id");
    }

    public async Task<IEnumerable<JobApplication>> GetByStatusAsync(ApplicationStatus status)
    {
        using var conn = _db.CreateConnection();

        var sql = """
            SELECT ja.*, c.*, ct.*
            FROM JobApplications ja
            JOIN Companies c ON c.Id = ja.CompanyId
            LEFT JOIN Contacts ct ON ct.Id = ja.ContactId
            WHERE ja.Status = @status
            ORDER BY ja.AppliedDate DESC
            """;

        return await conn.QueryAsync<JobApplication, Company, Contact, JobApplication>(
            sql,
            (ja, c, ct) => { ja.Company = c; ja.Contact = ct; return ja; },
            new { status = (int)status },
            splitOn: "Id,Id");
    }

    public async Task<IEnumerable<JobApplication>> GetByCompanyAsync(int companyId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<JobApplication>(
            "SELECT * FROM JobApplications WHERE CompanyId = @companyId ORDER BY AppliedDate DESC",
            new { companyId });
    }

    public async Task<JobApplication> AddAsync(JobApplication entity)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO JobApplications
                (RoleName, JobDescription, Status, AppliedDate, LastUpdated, JobPostingUrl,
                 SalaryRange, IsRemote, Notes, CompanyId, ContactId)
            VALUES
                (@RoleName, @JobDescription, @Status, @AppliedDate, @LastUpdated, @JobPostingUrl,
                 @SalaryRange, @IsRemote, @Notes, @CompanyId, @ContactId);
            SELECT last_insert_rowid();
            """,
            entity, tx);

        entity.Id = id;

        foreach (var appSkill in entity.ApplicationSkills)
        {
            appSkill.JobApplicationId = id;
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO ApplicationSkills (JobApplicationId, SkillId, IsOwned, IsRequired) VALUES (@JobApplicationId, @SkillId, @IsOwned, @IsRequired)",
                appSkill, tx);
        }

        tx.Commit();
        return entity;
    }

    public async Task UpdateAsync(JobApplication entity)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            """
            UPDATE JobApplications SET
                RoleName = @RoleName, JobDescription = @JobDescription, Status = @Status,
                LastUpdated = @LastUpdated, JobPostingUrl = @JobPostingUrl,
                SalaryRange = @SalaryRange, IsRemote = @IsRemote, Notes = @Notes,
                CompanyId = @CompanyId, ContactId = @ContactId
            WHERE Id = @Id
            """,
            entity, tx);

        await conn.ExecuteAsync(
            "DELETE FROM ApplicationSkills WHERE JobApplicationId = @id", new { id = entity.Id }, tx);

        foreach (var appSkill in entity.ApplicationSkills)
        {
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO ApplicationSkills (JobApplicationId, SkillId, IsOwned, IsRequired) VALUES (@JobApplicationId, @SkillId, @IsOwned, @IsRequired)",
                appSkill, tx);
        }

        tx.Commit();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM JobApplications WHERE Id = @id", new { id });
    }
}
