using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;

namespace JobTracker.Application.Services;

public class JobApplicationService : IJobApplicationService
{
    private readonly IJobApplicationRepository _repo;
    private readonly IMarkdownSyncService _markdownSync;

    /// <inheritdoc/>
    public event Action<string>? SyncWarning;

    public JobApplicationService(
        IJobApplicationRepository repo,
        IMarkdownSyncService markdownSync)
    {
        _repo = repo;
        _markdownSync = markdownSync;
    }

    public async Task<IEnumerable<JobApplicationDto>> GetCurrentWeekApplicationsAsync()
    {
        var isoWeek = GetIso8601WeekOfYear(DateTime.UtcNow);
        var apps = await _repo.GetByWeekAsync(isoWeek, DateTime.UtcNow.Year);
        return apps.Select(MapToDto);
    }

    public async Task<IEnumerable<JobApplicationDto>> GetAllApplicationsAsync()
    {
        var apps = await _repo.GetAllAsync();
        return apps.Select(MapToDto);
    }

    public async Task<JobApplicationDto?> GetByIdAsync(int id)
    {
        var app = await _repo.GetWithDetailsAsync(id);
        return app is null ? null : MapToDto(app);
    }

    public async Task<JobApplicationDto> CreateAsync(CreateJobApplicationRequest request)
    {
        var entity = new JobApplication
        {
            RoleName = request.RoleName.Trim(),
            JobDescription = request.JobDescription,
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            Status = request.Status,
            AppliedDate = request.AppliedDate,
            IsRemote = request.IsRemote,
            SalaryRange = request.SalaryRange?.Trim(),
            Notes = request.Notes?.Trim(),
            JobPostingUrl = request.JobPostingUrl?.Trim(),
            LastUpdated = DateTime.UtcNow,
            ApplicationSkills = request.SkillIds
                .Select(sid => new ApplicationSkill { SkillId = sid, IsRequired = true })
                .ToList()
        };

        var created = await _repo.AddAsync(entity);
        FireAndForgetSync(created.Id);
        return MapToDto(created);
    }

    public async Task UpdateAsync(UpdateJobApplicationRequest request)
    {
        var entity = await _repo.GetWithDetailsAsync(request.Id)
            ?? throw new KeyNotFoundException($"Application {request.Id} not found.");

        entity.RoleName = request.RoleName.Trim();
        entity.JobDescription = request.JobDescription;
        entity.CompanyId = request.CompanyId;
        entity.ContactId = request.ContactId;
        entity.Status = request.Status;
        entity.IsRemote = request.IsRemote;
        entity.SalaryRange = request.SalaryRange?.Trim();
        entity.Notes = request.Notes?.Trim();
        entity.JobPostingUrl = request.JobPostingUrl?.Trim();
        entity.LastUpdated = DateTime.UtcNow;

        entity.ApplicationSkills.Clear();
        foreach (var sid in request.SkillIds)
            entity.ApplicationSkills.Add(new ApplicationSkill { SkillId = sid, JobApplicationId = entity.Id });

        await _repo.UpdateAsync(entity);
        FireAndForgetSync(entity.Id);
    }

    public async Task DeleteAsync(int id)
    {
        // Get the application details to retrieve the markdown file name before deleting
        var app = await _repo.GetByIdAsync(id);

        // Delete from database first
        await _repo.DeleteAsync(id);

        // Then delete the markdown file if it exists
        if (app is not null)
        {
            try
            {
                var result = await _markdownSync.DeleteApplicationFileAsync(app.MarkdownFileName);
                if (!result.Success)
                    SyncWarning?.Invoke(result.ErrorMessage ?? "Failed to delete markdown file.");
            }
            catch (Exception ex)
            {
                SyncWarning?.Invoke($"Error deleting markdown file: {ex.Message}");
            }
        }
    }

    public async Task UpdateStatusAsync(int id, Domain.Enums.ApplicationStatus status)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Application {id} not found.");
        entity.Status = status;
        entity.LastUpdated = DateTime.UtcNow;
        await _repo.UpdateAsync(entity);
        FireAndForgetSync(entity.Id);
    }

    /// <summary>
    /// Runs markdown sync on a background thread without blocking the caller.
    /// Surfaces any failure via the SyncWarning event (invoked on the thread pool).
    /// </summary>
    private void FireAndForgetSync(int applicationId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _markdownSync.SyncApplicationAsync(applicationId);
                if (!result.Success)
                    SyncWarning?.Invoke(result.ErrorMessage ?? "Markdown sync failed.");
            }
            catch (Exception ex)
            {
                SyncWarning?.Invoke($"Markdown sync error: {ex.Message}");
            }
        });
    }

    private static JobApplicationDto MapToDto(JobApplication a) => new(
        a.Id,
        a.RoleName,
        a.Company?.Name ?? "Unknown",
        a.Status,
        a.AppliedDate,
        a.DaysSinceApplication,
        a.Contact?.Name,
        a.Contact?.LinkedInUrl,
        a.ApplicationSkills.Select(s => s.Skill?.Name ?? "").Where(n => n != "").ToList(),
        a.Notes,
        a.IsRemote,
        a.SalaryRange
    );

    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            date = date.AddDays(3);
        return System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}
