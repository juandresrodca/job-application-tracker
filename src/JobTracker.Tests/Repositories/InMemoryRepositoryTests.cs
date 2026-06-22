using FluentAssertions;
using Xunit;
using Microsoft.Data.Sqlite;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Repositories;

namespace JobTracker.Tests.Repositories;

/// <summary>
/// Tests Dapper SQL and mapping against a real in-memory SQLite database.
/// These are the highest-value tests — they validate both SQL correctness and entity mapping.
/// </summary>
public class InMemoryRepositoryTests : IAsyncLifetime
{
    private DatabaseContext _db = null!;
    private SqliteConnection _keepAlive = null!;
    private CompanyRepository _companyRepo = null!;
    private ContactRepository _contactRepo = null!;
    private SkillRepository _skillRepo = null!;
    private JobApplicationRepository _appRepo = null!;

    public async Task InitializeAsync()
    {
        // Named shared-cache in-memory DB: all connections with the same name share the schema.
        var dbName = $"testdb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _db = new DatabaseContext(dbName);
        // Hold one connection open so the named in-memory database survives across repository calls.
        _keepAlive = _db.CreateConnection();
        await _keepAlive.OpenAsync();
        await _db.InitializeAsync();

        _companyRepo = new CompanyRepository(_db);
        _contactRepo = new ContactRepository(_db);
        _skillRepo   = new SkillRepository(_db);
        _appRepo     = new JobApplicationRepository(_db);
    }

    public Task DisposeAsync()
    {
        _keepAlive.Dispose();
        return Task.CompletedTask;
    }

    // ── Company ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompanyRepository_AddAndGetById_Works()
    {
        var company = await _companyRepo.AddAsync(new Company { Name = "Acme Corp", Industry = "Tech" });

        company.Id.Should().BeGreaterThan(0);

        var loaded = await _companyRepo.GetByIdAsync(company.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Acme Corp");
        loaded.Industry.Should().Be("Tech");
    }

    [Fact]
    public async Task CompanyRepository_GetByName_IsCaseInsensitive()
    {
        await _companyRepo.AddAsync(new Company { Name = "Google LLC" });

        var result = await _companyRepo.GetByNameAsync("google llc");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Google LLC");
    }

    [Fact]
    public async Task CompanyRepository_Delete_RemovesRecord()
    {
        var c = await _companyRepo.AddAsync(new Company { Name = "To Delete" });
        await _companyRepo.DeleteAsync(c.Id);

        var loaded = await _companyRepo.GetByIdAsync(c.Id);
        loaded.Should().BeNull();
    }

    // ── Contact ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ContactRepository_AddAndGetByCompany_Works()
    {
        var company = await _companyRepo.AddAsync(new Company { Name = "Meta" });
        await _contactRepo.AddAsync(new Contact { Name = "Jane Doe", CompanyId = company.Id, Email = "jane@meta.com" });
        await _contactRepo.AddAsync(new Contact { Name = "John Smith", CompanyId = company.Id });

        var contacts = (await _contactRepo.GetByCompanyAsync(company.Id)).ToList();

        contacts.Should().HaveCount(2);
        contacts.Should().Contain(c => c.Name == "Jane Doe");
        contacts.First(c => c.Name == "Jane Doe").Email.Should().Be("jane@meta.com");
    }

    // ── Skill ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SkillRepository_AddAndGetAll_ReturnsOrderedByCategoryThenName()
    {
        await _skillRepo.AddAsync(new Skill { Name = "Python",    Category = "Scripting" });
        await _skillRepo.AddAsync(new Skill { Name = "Azure",     Category = "Cloud" });
        await _skillRepo.AddAsync(new Skill { Name = "PowerShell",Category = "Scripting" });

        var all = (await _skillRepo.GetAllAsync()).ToList();

        all.Should().HaveCount(3);
        all[0].Category.Should().Be("Cloud");     // Cloud comes before Scripting
        all[1].Category.Should().Be("Scripting");
        all[1].Name.Should().Be("PowerShell");    // P before Py alphabetically
    }

    // ── JobApplication ────────────────────────────────────────────────────────

    [Fact]
    public async Task JobApplicationRepository_AddAndGetWithDetails_IncludesCompany()
    {
        var company = await _companyRepo.AddAsync(new Company { Name = "Microsoft" });

        var app = await _appRepo.AddAsync(new JobApplication
        {
            RoleName = "Cloud Architect",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
            CompanyId = company.Id,
            ApplicationSkills = new List<ApplicationSkill>()
        });

        var loaded = await _appRepo.GetWithDetailsAsync(app.Id);

        loaded.Should().NotBeNull();
        loaded!.RoleName.Should().Be("Cloud Architect");
        loaded.Company.Should().NotBeNull();
        loaded.Company!.Name.Should().Be("Microsoft");
    }

    [Fact]
    public async Task JobApplicationRepository_GetAll_ReturnsAllAdded()
    {
        var company = await _companyRepo.AddAsync(new Company { Name = "Test Co" });

        await _appRepo.AddAsync(new JobApplication
        {
            RoleName = "Dev 1", CompanyId = company.Id,
            Status = ApplicationStatus.Applied, AppliedDate = DateTime.UtcNow,
            ApplicationSkills = new List<ApplicationSkill>()
        });
        await _appRepo.AddAsync(new JobApplication
        {
            RoleName = "Dev 2", CompanyId = company.Id,
            Status = ApplicationStatus.Interview, AppliedDate = DateTime.UtcNow,
            ApplicationSkills = new List<ApplicationSkill>()
        });

        var all = await _appRepo.GetAllAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task JobApplicationRepository_Delete_RemovesApplication()
    {
        var company = await _companyRepo.AddAsync(new Company { Name = "Del Co" });
        var app = await _appRepo.AddAsync(new JobApplication
        {
            RoleName = "To Delete", CompanyId = company.Id,
            Status = ApplicationStatus.Rejected, AppliedDate = DateTime.UtcNow,
            ApplicationSkills = new List<ApplicationSkill>()
        });

        await _appRepo.DeleteAsync(app.Id);

        var loaded = await _appRepo.GetByIdAsync(app.Id);
        loaded.Should().BeNull();
    }
}
