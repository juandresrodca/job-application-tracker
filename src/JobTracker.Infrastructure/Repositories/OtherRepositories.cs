using Dapper;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Data;

namespace JobTracker.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly DatabaseContext _db;
    public CompanyRepository(DatabaseContext db) => _db = db;

    public async Task<Company?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Company>("SELECT * FROM Companies WHERE Id = @id", new { id });
    }

    public async Task<IEnumerable<Company>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Company>("SELECT * FROM Companies ORDER BY Name");
    }

    public async Task<Company?> GetByNameAsync(string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Company>(
            "SELECT * FROM Companies WHERE Name = @name COLLATE NOCASE", new { name });
    }

    public async Task<Company> AddAsync(Company entity)
    {
        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Companies (Name, Website, Industry, Location, Notes) VALUES (@Name, @Website, @Industry, @Location, @Notes); SELECT last_insert_rowid();",
            entity);
        entity.Id = id;
        return entity;
    }

    public async Task UpdateAsync(Company entity)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Companies SET Name=@Name, Website=@Website, Industry=@Industry, Location=@Location, Notes=@Notes WHERE Id=@Id",
            entity);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Companies WHERE Id=@id", new { id });
    }
}

public class ContactRepository : IContactRepository
{
    private readonly DatabaseContext _db;
    public ContactRepository(DatabaseContext db) => _db = db;

    public async Task<Contact?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Contact>("SELECT * FROM Contacts WHERE Id=@id", new { id });
    }

    public async Task<IEnumerable<Contact>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Contact>("SELECT * FROM Contacts ORDER BY Name");
    }

    public async Task<IEnumerable<Contact>> GetByCompanyAsync(int companyId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Contact>("SELECT * FROM Contacts WHERE CompanyId=@companyId", new { companyId });
    }

    public async Task<Contact> AddAsync(Contact entity)
    {
        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Contacts (Name, Email, Phone, LinkedInUrl, Role, Notes, CompanyId) VALUES (@Name, @Email, @Phone, @LinkedInUrl, @Role, @Notes, @CompanyId); SELECT last_insert_rowid();",
            entity);
        entity.Id = id;
        return entity;
    }

    public async Task UpdateAsync(Contact entity)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Contacts SET Name=@Name, Email=@Email, Phone=@Phone, LinkedInUrl=@LinkedInUrl, Role=@Role, Notes=@Notes, CompanyId=@CompanyId WHERE Id=@Id",
            entity);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Contacts WHERE Id=@id", new { id });
    }
}

public class SkillRepository : ISkillRepository
{
    private readonly DatabaseContext _db;
    public SkillRepository(DatabaseContext db) => _db = db;

    public async Task<Skill?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Skill>("SELECT * FROM Skills WHERE Id=@id", new { id });
    }

    public async Task<IEnumerable<Skill>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Skill>("SELECT * FROM Skills ORDER BY Category, Name");
    }

    public async Task<Skill?> GetByNameAsync(string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Skill>(
            "SELECT * FROM Skills WHERE Name=@name COLLATE NOCASE", new { name });
    }

    public async Task<IEnumerable<Skill>> GetByApplicationAsync(int applicationId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Skill>(
            "SELECT s.* FROM Skills s JOIN ApplicationSkills aps ON aps.SkillId=s.Id WHERE aps.JobApplicationId=@applicationId",
            new { applicationId });
    }

    public async Task<Skill> AddAsync(Skill entity)
    {
        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Skills (Name, Category) VALUES (@Name, @Category); SELECT last_insert_rowid();", entity);
        entity.Id = id;
        return entity;
    }

    public async Task UpdateAsync(Skill entity)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("UPDATE Skills SET Name=@Name, Category=@Category WHERE Id=@Id", entity);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Skills WHERE Id=@id", new { id });
    }
}
