using System.Collections.ObjectModel;
using JobTracker.Application.DTOs;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.Services;

namespace JobTracker.WPF.ViewModels;

public class ContactsViewModel : ViewModelBase, IRefreshable
{
    private readonly IContactRepository _contactRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IDialogService _dialog;

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<ContactDto> Contacts { get; } = new();
    public ObservableCollection<ContactDto> FilteredContacts { get; } = new();
    public ObservableCollection<CompanyDto> Companies { get; } = new();

    // ── Selection / form ─────────────────────────────────────────────────────
    private ContactDto? _selectedContact;
    public ContactDto? SelectedContact
    {
        get => _selectedContact;
        set { SetField(ref _selectedContact, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selectedContact is not null;

    private bool _isFormVisible;
    public bool IsFormVisible { get => _isFormVisible; set => SetField(ref _isFormVisible, value); }

    private bool _isEditing;

    // ── Form fields ──────────────────────────────────────────────────────────
    private string _editName = string.Empty;
    public string EditName
    {
        get => _editName;
        set { SetField(ref _editName, value); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private string _editEmail = string.Empty;
    public string EditEmail { get => _editEmail; set => SetField(ref _editEmail, value); }

    private string _editPhone = string.Empty;
    public string EditPhone { get => _editPhone; set => SetField(ref _editPhone, value); }

    private string _editLinkedIn = string.Empty;
    public string EditLinkedIn
    {
        get => _editLinkedIn;
        set
        {
            SetField(ref _editLinkedIn, value);
            LinkedInError = IsValidLinkedInUrl(value) ? string.Empty : "Enter a valid linkedin.com URL";
        }
    }

    private string _linkedInError = string.Empty;
    public string LinkedInError { get => _linkedInError; set => SetField(ref _linkedInError, value); }

    private string _editRole = string.Empty;
    public string EditRole { get => _editRole; set => SetField(ref _editRole, value); }

    private string _editNotes = string.Empty;
    public string EditNotes { get => _editNotes; set => SetField(ref _editNotes, value); }

    private CompanyDto? _editCompany;
    public CompanyDto? EditCompany { get => _editCompany; set => SetField(ref _editCompany, value); }

    // ── Search ───────────────────────────────────────────────────────────────
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { SetField(ref _searchText, value); ApplyFilter(); }
    }

    // ── Status ───────────────────────────────────────────────────────────────
    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    // ── Commands ─────────────────────────────────────────────────────────────
    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }

    public ContactsViewModel(IContactRepository contactRepo, ICompanyRepository companyRepo, IDialogService dialog)
    {
        _contactRepo = contactRepo;
        _companyRepo = companyRepo;
        _dialog = dialog;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        NewCommand = new RelayCommand(OpenNewForm);
        EditCommand = new RelayCommand(OpenEditForm, () => HasSelection);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !string.IsNullOrWhiteSpace(EditName) && string.IsNullOrEmpty(LinkedInError));
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => HasSelection);
        CancelCommand = new RelayCommand(() => IsFormVisible = false);
    }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var contacts = await _contactRepo.GetAllAsync();
            Contacts.Clear();
            foreach (var c in contacts)
                Contacts.Add(new ContactDto(c.Id, c.Name, c.Email, c.LinkedInUrl, c.Role, c.CompanyId));

            var companies = await _companyRepo.GetAllAsync();
            Companies.Clear();
            foreach (var c in companies)
                Companies.Add(new CompanyDto(c.Id, c.Name, c.Website, c.Industry, c.Location));

            ApplyFilter();
            StatusMessage = $"{Contacts.Count} contacts";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredContacts.Clear();
        var query = Contacts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var t = SearchText.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(t) ||
                (c.Email?.ToLower().Contains(t) ?? false) ||
                (c.Role?.ToLower().Contains(t) ?? false));
        }
        foreach (var c in query.OrderBy(c => c.Name))
            FilteredContacts.Add(c);
    }

    private void OpenNewForm()
    {
        _isEditing = false;
        EditName = EditEmail = EditPhone = EditLinkedIn = EditRole = EditNotes = string.Empty;
        LinkedInError = string.Empty;
        EditCompany = null;
        IsFormVisible = true;
    }

    private void OpenEditForm()
    {
        if (_selectedContact is null) return;
        _isEditing = true;
        EditName = _selectedContact.Name;
        EditEmail = _selectedContact.Email ?? string.Empty;
        EditLinkedIn = _selectedContact.LinkedInUrl ?? string.Empty;
        LinkedInError = string.Empty;
        EditRole = _selectedContact.Role ?? string.Empty;
        EditPhone = string.Empty;
        EditNotes = string.Empty;
        EditCompany = _selectedContact.CompanyId.HasValue
            ? Companies.FirstOrDefault(c => c.Id == _selectedContact.CompanyId.Value)
            : null;
        IsFormVisible = true;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (_isEditing && _selectedContact is not null)
            {
                var entity = new Contact
                {
                    Id = _selectedContact.Id,
                    Name = EditName.Trim(),
                    Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
                    LinkedInUrl = string.IsNullOrWhiteSpace(EditLinkedIn) ? null : EditLinkedIn.Trim(),
                    Role = string.IsNullOrWhiteSpace(EditRole) ? null : EditRole.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
                    CompanyId = EditCompany?.Id,
                };
                await _contactRepo.UpdateAsync(entity);
                StatusMessage = "Contact updated.";
            }
            else
            {
                var entity = new Contact
                {
                    Name = EditName.Trim(),
                    Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
                    LinkedInUrl = string.IsNullOrWhiteSpace(EditLinkedIn) ? null : EditLinkedIn.Trim(),
                    Role = string.IsNullOrWhiteSpace(EditRole) ? null : EditRole.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
                    CompanyId = EditCompany?.Id,
                };
                await _contactRepo.AddAsync(entity);
                StatusMessage = "Contact added.";
            }
            IsFormVisible = false;
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync()
    {
        if (_selectedContact is null) return;
        if (!_dialog.Confirm("Delete Contact", $"Delete \"{_selectedContact.Name}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _contactRepo.DeleteAsync(_selectedContact.Id);
            StatusMessage = "Contact deleted.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Delete failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private static bool IsValidLinkedInUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true; // optional
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && uri.Host.EndsWith("linkedin.com", StringComparison.OrdinalIgnoreCase);
    }
}
