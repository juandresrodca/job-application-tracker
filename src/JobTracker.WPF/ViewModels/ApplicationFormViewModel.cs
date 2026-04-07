using System;
using System.Collections.ObjectModel;
using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Domain.Interfaces;
using JobTracker.WPF.Services;

namespace JobTracker.WPF.ViewModels;

public class ApplicationFormViewModel : ViewModelBase
{
    private readonly IJobApplicationService _appService;
    private readonly ICompanyRepository _companyRepo;
    private readonly IContactRepository _contactRepo;
    private readonly ISkillRepository _skillRepo;
    private readonly ISettingsService _settings;
    private readonly IPdfExtractionService _pdfExtraction;
    private readonly IDialogService _dialogService;

    private int? _editingId;
    public bool IsEditing => _editingId.HasValue;
    public string FormTitle => IsEditing ? "View/Edit Application" : "New Application";

    // ── Form fields ──────────────────────────────────────────────────────────
    private string _roleName = string.Empty;
    public string RoleName
    {
        get => _roleName;
        set
        {
            SetField(ref _roleName, value);
            OnPropertyChanged(nameof(HasRoleNameError));
            OnPropertyChanged(nameof(RoleNameError));
        }
    }

    private string _jobDescription = string.Empty;
    public string JobDescription { get => _jobDescription; set => SetField(ref _jobDescription, value); }

    private CompanyDto? _selectedCompany;
    public CompanyDto? SelectedCompany
    {
        get => _selectedCompany;
        set
        {
            SetField(ref _selectedCompany, value);
            OnPropertyChanged(nameof(HasCompanyError));
            OnPropertyChanged(nameof(CompanyError));
        }
    }

    private ContactDto? _selectedContact;
    public ContactDto? SelectedContact { get => _selectedContact; set => SetField(ref _selectedContact, value); }

    private ApplicationStatus _status = ApplicationStatus.Applied;
    public ApplicationStatus Status { get => _status; set => SetField(ref _status, value); }

    private DateTime _appliedDate = DateTime.Today;
    public DateTime AppliedDate { get => _appliedDate; set => SetField(ref _appliedDate, value); }

    private bool _isRemote;
    public bool IsRemote { get => _isRemote; set => SetField(ref _isRemote, value); }

    private string _salaryRange = string.Empty;
    public string SalaryRange { get => _salaryRange; set => SetField(ref _salaryRange, value); }

    private string _notes = string.Empty;
    public string Notes { get => _notes; set => SetField(ref _notes, value); }

    private string _jobPostingUrl = string.Empty;
    public string JobPostingUrl { get => _jobPostingUrl; set => SetField(ref _jobPostingUrl, value); }

    private string _newCompanyName = string.Empty;
    public string NewCompanyName { get => _newCompanyName; set => SetField(ref _newCompanyName, value); }

    private string _newCompanyWebsite = string.Empty;
    public string NewCompanyWebsite { get => _newCompanyWebsite; set => SetField(ref _newCompanyWebsite, value); }

    private string _newCompanyIndustry = string.Empty;
    public string NewCompanyIndustry { get => _newCompanyIndustry; set => SetField(ref _newCompanyIndustry, value); }

    private string _newCompanyLocation = string.Empty;
    public string NewCompanyLocation { get => _newCompanyLocation; set => SetField(ref _newCompanyLocation, value); }

    private bool _isCreatingNewCompany = false;
    public bool IsCreatingNewCompany { get => _isCreatingNewCompany; set => SetField(ref _isCreatingNewCompany, value); }

    private string _newContactName = string.Empty;
    public string NewContactName { get => _newContactName; set => SetField(ref _newContactName, value); }

    private string _newContactEmail = string.Empty;
    public string NewContactEmail { get => _newContactEmail; set => SetField(ref _newContactEmail, value); }

    private string _newContactRole = string.Empty;
    public string NewContactRole { get => _newContactRole; set => SetField(ref _newContactRole, value); }

    private bool _isCreatingNewContact = false;
    public bool IsCreatingNewContact { get => _isCreatingNewContact; set => SetField(ref _isCreatingNewContact, value); }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<CompanyDto> Companies { get; } = new();
    public ObservableCollection<ContactDto> Contacts { get; } = new();
    public ObservableCollection<SkillSelectionItem> Skills { get; } = new();
    public IEnumerable<ApplicationStatus> AllStatuses => Enum.GetValues<ApplicationStatus>();

    // ── Validation ─────────────────────────────────────────────────────────
    public bool HasRoleNameError => string.IsNullOrWhiteSpace(RoleName);
    public bool HasCompanyError => SelectedCompany is null;
    public string RoleNameError => HasRoleNameError ? "Role name is required" : string.Empty;
    public string CompanyError => HasCompanyError ? "Company selection is required" : string.Empty;

    // ── Commands ─────────────────────────────────────────────────────────────
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand LoadReferenceDataCommand { get; }
    public AsyncRelayCommand LoadFromPdfCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ShowCreateCompanyDialogCommand { get; }
    public AsyncRelayCommand CreateNewCompanyCommand { get; }
    public RelayCommand CancelCreateCompanyCommand { get; }
    public RelayCommand ShowCreateContactDialogCommand { get; }
    public AsyncRelayCommand CreateNewContactCommand { get; }
    public RelayCommand CancelCreateContactCommand { get; }

    // ── Events ───────────────────────────────────────────────────────────────
    public event Action? SaveCompleted;
    public event Action? Cancelled;

    public ApplicationFormViewModel(
        IJobApplicationService appService,
        ICompanyRepository companyRepo,
        IContactRepository contactRepo,
        ISkillRepository skillRepo,
        ISettingsService settings,
        IPdfExtractionService pdfExtraction,
        IDialogService dialogService)
    {
        _appService = appService;
        _companyRepo = companyRepo;
        _contactRepo = contactRepo;
        _skillRepo = skillRepo;
        _settings = settings;
        _pdfExtraction = pdfExtraction;
        _dialogService = dialogService;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !string.IsNullOrWhiteSpace(RoleName) && SelectedCompany is not null);
        LoadReferenceDataCommand = new AsyncRelayCommand(LoadReferenceDataAsync);
        LoadFromPdfCommand = new AsyncRelayCommand(LoadFromPdfAsync);
        CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
        ShowCreateCompanyDialogCommand = new RelayCommand(() => IsCreatingNewCompany = true);
        CreateNewCompanyCommand = new AsyncRelayCommand(CreateNewCompanyAsync, () => !string.IsNullOrWhiteSpace(NewCompanyName));
        CancelCreateCompanyCommand = new RelayCommand(() =>
        {
            IsCreatingNewCompany = false;
            NewCompanyName = string.Empty;
            NewCompanyWebsite = string.Empty;
            NewCompanyIndustry = string.Empty;
            NewCompanyLocation = string.Empty;
        });
        ShowCreateContactDialogCommand = new RelayCommand(() => IsCreatingNewContact = true);
        CreateNewContactCommand = new AsyncRelayCommand(CreateNewContactAsync, () => !string.IsNullOrWhiteSpace(NewContactName) && SelectedCompany is not null);
        CancelCreateContactCommand = new RelayCommand(() =>
        {
            IsCreatingNewContact = false;
            NewContactName = string.Empty;
            NewContactEmail = string.Empty;
            NewContactRole = string.Empty;
        });
    }

    public async Task InitializeForEditAsync(int applicationId)
    {
        _editingId = applicationId;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        await LoadReferenceDataAsync();

        var app = await _appService.GetByIdAsync(applicationId);
        if (app is null) return;

        RoleName = app.RoleName;
        Status = app.Status;
        AppliedDate = app.AppliedDate;
        IsRemote = app.IsRemote;
        SalaryRange = app.SalaryRange ?? string.Empty;
        Notes = app.Notes ?? string.Empty;

        SelectedCompany = Companies.FirstOrDefault(c => c.Name == app.CompanyName);
        if (app.ContactName is not null)
            SelectedContact = Contacts.FirstOrDefault(c => c.Name == app.ContactName);

        foreach (var skill in Skills)
            skill.IsSelected = app.Skills.Contains(skill.Name);
    }

    public async Task InitializeForCreateAsync()
    {
        await LoadReferenceDataAsync();
    }

    private async Task LoadReferenceDataAsync()
    {
        var companies = await _companyRepo.GetAllAsync();
        Companies.Clear();
        foreach (var c in companies)
            Companies.Add(new CompanyDto(c.Id, c.Name, c.Website, c.Industry, c.Location));

        var contacts = await _contactRepo.GetAllAsync();
        Contacts.Clear();
        foreach (var c in contacts)
            Contacts.Add(new ContactDto(c.Id, c.Name, c.Email, c.LinkedInUrl, c.Role, c.CompanyId));

        var skills = await _skillRepo.GetAllAsync();
        Skills.Clear();
        foreach (var s in skills)
            Skills.Add(new SkillSelectionItem(s.Id, s.Name, s.Category ?? string.Empty));
    }

    private async Task SaveAsync()
    {
        try
        {
            var selectedSkillIds = Skills.Where(s => s.IsSelected).Select(s => s.Id).ToList();

            if (IsEditing)
            {
                await _appService.UpdateAsync(new UpdateJobApplicationRequest(
                    _editingId!.Value, RoleName, JobDescription,
                    SelectedCompany!.Id, SelectedContact?.Id, Status,
                    IsRemote, SalaryRange, Notes, JobPostingUrl, selectedSkillIds));
            }
            else
            {
                await _appService.CreateAsync(new CreateJobApplicationRequest(
                    RoleName, JobDescription, SelectedCompany!.Id, SelectedContact?.Id,
                    Status, AppliedDate, IsRemote, SalaryRange, Notes, JobPostingUrl,
                    selectedSkillIds));
            }

            SaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            // Error handling could be improved, but for now we just catch it
            System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
        }
    }

    private async Task LoadFromPdfAsync()
    {
        try
        {
            var filePath = _dialogService.SelectFile("Select PDF File", "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*");
            if (string.IsNullOrEmpty(filePath))
                return;

            // Extract all text for job description
            var extractedText = await _pdfExtraction.ExtractTextAsync(filePath);
            JobDescription = extractedText;

            // Extract company name
            var companyName = await _pdfExtraction.ExtractCompanyNameAsync(filePath);
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var company = Companies.FirstOrDefault(c => c.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase));
                if (company is not null)
                {
                    SelectedCompany = company;
                }
                else
                {
                    // Ask user if they want to create the company
                    var createCompany = _dialogService.Confirm("Company Not Found",
                        $"Company '{companyName}' not found. Would you like to create it?");
                    if (createCompany)
                    {
                        var newCompany = new Company { Name = companyName.Trim() };
                        var createdCompany = await _companyRepo.AddAsync(newCompany);
                        var companyDto = new CompanyDto(
                            createdCompany.Id,
                            createdCompany.Name,
                            createdCompany.Website,
                            createdCompany.Industry,
                            createdCompany.Location);
                        Companies.Add(companyDto);
                        SelectedCompany = companyDto;
                    }
                }
            }

            // Extract role name
            var roleName = await _pdfExtraction.ExtractRoleNameAsync(filePath);
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                RoleName = roleName;
            }

            _dialogService.Alert("Success", "PDF information extracted and loaded.");
        }
        catch (System.IO.FileNotFoundException)
        {
            _dialogService.Alert("Error", "The selected PDF file could not be found.");
        }
        catch (Exception ex)
        {
            _dialogService.Alert("Error", $"Failed to extract text from PDF: {ex.Message}");
        }
    }

    private async Task CreateNewCompanyAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCompanyName))
        {
            _dialogService.Alert("Error", "Company name is required.");
            return;
        }

        try
        {
            var newCompany = new Company
            {
                Name = NewCompanyName.Trim(),
                Website = string.IsNullOrWhiteSpace(NewCompanyWebsite) ? null : NewCompanyWebsite.Trim(),
                Industry = string.IsNullOrWhiteSpace(NewCompanyIndustry) ? null : NewCompanyIndustry.Trim(),
                Location = string.IsNullOrWhiteSpace(NewCompanyLocation) ? null : NewCompanyLocation.Trim()
            };

            var createdCompany = await _companyRepo.AddAsync(newCompany);

            // Add to the UI collections
            var companyDto = new CompanyDto(
                createdCompany.Id,
                createdCompany.Name,
                createdCompany.Website,
                createdCompany.Industry,
                createdCompany.Location);

            Companies.Add(companyDto);
            SelectedCompany = companyDto;

            // Close dialog and reset fields
            IsCreatingNewCompany = false;
            NewCompanyName = string.Empty;
            NewCompanyWebsite = string.Empty;
            NewCompanyIndustry = string.Empty;
            NewCompanyLocation = string.Empty;

            _dialogService.Alert("Success", $"Company '{newCompany.Name}' created successfully.");
        }
        catch (Exception ex)
        {
            _dialogService.Alert("Error", $"Failed to create company: {ex.Message}");
        }
    }

    private async Task CreateNewContactAsync()
    {
        if (string.IsNullOrWhiteSpace(NewContactName) || SelectedCompany is null)
        {
            _dialogService.Alert("Error", "Contact name and company are required.");
            return;
        }

        try
        {
            var newContact = new Contact
            {
                Name = NewContactName.Trim(),
                Email = string.IsNullOrWhiteSpace(NewContactEmail) ? null : NewContactEmail.Trim(),
                Role = string.IsNullOrWhiteSpace(NewContactRole) ? null : NewContactRole.Trim(),
                CompanyId = SelectedCompany.Id
            };

            var createdContact = await _contactRepo.AddAsync(newContact);

            // Add to the UI collections
            var contactDto = new ContactDto(
                createdContact.Id,
                createdContact.Name,
                createdContact.Email,
                createdContact.LinkedInUrl,
                createdContact.Role,
                createdContact.CompanyId);

            Contacts.Add(contactDto);
            SelectedContact = contactDto;

            // Close dialog and reset fields
            IsCreatingNewContact = false;
            NewContactName = string.Empty;
            NewContactEmail = string.Empty;
            NewContactRole = string.Empty;

            _dialogService.Alert("Success", $"Contact '{newContact.Name}' created successfully.");
        }
        catch (Exception ex)
        {
            _dialogService.Alert("Error", $"Failed to create contact: {ex.Message}");
        }
    }
}

/// <summary>Helper item for skill checkboxes in the form.</summary>
public class SkillSelectionItem : ViewModelBase
{
    public int Id { get; }
    public string Name { get; }
    public string Category { get; }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

    public SkillSelectionItem(int id, string name, string category)
    {
        Id = id; Name = name; Category = category;
    }
}
