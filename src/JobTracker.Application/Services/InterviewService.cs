using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;

namespace JobTracker.Application.Services;

public class InterviewService : IInterviewService
{
    private readonly IInterviewRepository _repo;
    private readonly IJobApplicationRepository _appRepo;

    public InterviewService(IInterviewRepository repo, IJobApplicationRepository appRepo)
    {
        _repo = repo;
        _appRepo = appRepo;
    }

    public async Task<IReadOnlyList<InterviewDto>> GetUpcomingAsync(int days = 14)
    {
        var interviews = await _repo.GetUpcomingAsync(days);
        return interviews.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<InterviewDto>> GetForMonthAsync(int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var interviews = await _repo.GetBetweenAsync(from, from.AddMonths(1));
        return interviews.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<InterviewDto>> GetByApplicationAsync(int applicationId)
    {
        var interviews = await _repo.GetByApplicationAsync(applicationId);

        // Per-application rows come without the join — resolve names once for all rows.
        var app = await _appRepo.GetWithDetailsAsync(applicationId);
        var role = app?.RoleName ?? "";
        var company = app?.Company?.Name ?? "";

        return interviews
            .Select(i => MapToDto(i, role, company))
            .OrderBy(i => i.ScheduledAt)
            .ToList();
    }

    public async Task<IReadOnlyList<InterviewDto>> GetAllAsync()
    {
        // Repo GetAllAsync loads the owning application + company, so MapToDto resolves names.
        var interviews = await _repo.GetAllAsync();
        return interviews.Select(MapToDto).ToList();
    }

    public async Task<InterviewDto> CreateAsync(CreateInterviewRequest request)
    {
        if (request.ScheduledAt == default)
            throw new ArgumentException("Interview date/time is required.", nameof(request));

        var entity = new Interview
        {
            JobApplicationId = request.JobApplicationId,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes <= 0 ? 60 : request.DurationMinutes,
            Type = request.Type,
            Interviewer = Clean(request.Interviewer),
            LocationOrLink = Clean(request.LocationOrLink),
            Notes = Clean(request.Notes),
        };

        var created = await _repo.AddAsync(entity);

        var app = await _appRepo.GetWithDetailsAsync(request.JobApplicationId);
        return MapToDto(created, app?.RoleName ?? "", app?.Company?.Name ?? "");
    }

    public async Task UpdateAsync(UpdateInterviewRequest request)
    {
        var entity = await _repo.GetByIdAsync(request.Id)
            ?? throw new KeyNotFoundException($"Interview {request.Id} not found.");

        entity.ScheduledAt = request.ScheduledAt;
        entity.DurationMinutes = request.DurationMinutes <= 0 ? 60 : request.DurationMinutes;
        entity.Type = request.Type;
        entity.Interviewer = Clean(request.Interviewer);
        entity.LocationOrLink = Clean(request.LocationOrLink);
        entity.Notes = Clean(request.Notes);
        entity.IsCompleted = request.IsCompleted;

        await _repo.UpdateAsync(entity);
    }

    public Task DeleteAsync(int id) => _repo.DeleteAsync(id);

    public async Task SetCompletedAsync(int id, bool completed)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Interview {id} not found.");
        entity.IsCompleted = completed;
        await _repo.UpdateAsync(entity);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InterviewDto MapToDto(Interview i) =>
        MapToDto(i, i.JobApplication?.RoleName ?? "", i.JobApplication?.Company?.Name ?? "");

    private static InterviewDto MapToDto(Interview i, string roleName, string companyName) => new(
        i.Id,
        i.JobApplicationId,
        roleName,
        companyName,
        i.ScheduledAt,
        i.DurationMinutes,
        i.Type,
        i.Interviewer,
        i.LocationOrLink,
        i.Notes,
        i.IsCompleted);
}
