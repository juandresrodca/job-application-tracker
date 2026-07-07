using JobTracker.Domain.Entities;

namespace JobTracker.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public interface IJobApplicationRepository : IRepository<JobApplication>
{
    Task<IEnumerable<JobApplication>> GetByWeekAsync(int isoWeek, int year);
    Task<IEnumerable<JobApplication>> GetByStatusAsync(Domain.Enums.ApplicationStatus status);
    Task<IEnumerable<JobApplication>> GetByCompanyAsync(int companyId);
    Task<JobApplication?> GetWithDetailsAsync(int id); // Includes Company, Contact, Skills
    Task<IEnumerable<JobApplication>> GetAllWithDetailsAsync(); // Loads all with Company, Contact, Skills (no N+1)
}

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetByNameAsync(string name);
}

public interface IContactRepository : IRepository<Contact>
{
    Task<IEnumerable<Contact>> GetByCompanyAsync(int companyId);
}

public interface ISkillRepository : IRepository<Skill>
{
    Task<Skill?> GetByNameAsync(string name);
    Task<IEnumerable<Skill>> GetByApplicationAsync(int applicationId);
}

public interface IInterviewRepository : IRepository<Interview>
{
    /// <summary>Interviews for one application, ordered by date.</summary>
    Task<IEnumerable<Interview>> GetByApplicationAsync(int applicationId);

    /// <summary>Pending interviews from now up to <paramref name="days"/> days ahead, with application/company loaded.</summary>
    Task<IEnumerable<Interview>> GetUpcomingAsync(int days);

    /// <summary>All interviews in a date range (calendar month view), with application/company loaded.</summary>
    Task<IEnumerable<Interview>> GetBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
}
