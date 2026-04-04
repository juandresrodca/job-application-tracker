using System.Collections.ObjectModel;
using JobTracker.Application.DTOs;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.Services;

namespace JobTracker.WPF.ViewModels;

public class CompaniesViewModel : ViewModelBase, IRefreshable
{
    private readonly ICompanyRepository _repo;
    private readonly IDialogService _dialog;

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<CompanyDto> Companies { get; } = new();
    public ObservableCollection<CompanyDto> FilteredCompanies { get; } = new();

    // ── Selection / form visibility ──────────────────────────────────────────
    private CompanyDto? _selectedCompany;
    public CompanyDto? SelectedCompany
    {
        get => _selectedCompany;
        set { SetField(ref _selectedCompany, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selectedCompany is not null;

    private bool _isFormVisible;
    public bool IsFormVisible
    {
        get => _isFormVisible;
        set => SetField(ref _isFormVisible, value);
    }

    private bool _isEditing;

    // ── Inline form fields ───────────────────────────────────────────────────
    private string _editName = string.Empty;
    public string EditName
    {
        get => _editName;
        set { SetField(ref _editName, value); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private string _editWebsite = string.Empty;
    public string EditWebsite { get => _editWebsite; set => SetField(ref _editWebsite, value); }

    private string _editIndustry = string.Empty;
    public string EditIndustry { get => _editIndustry; set => SetField(ref _editIndustry, value); }

    private string _editLocation = string.Empty;
    public string EditLocation { get => _editLocation; set => SetField(ref _editLocation, value); }

    private string _editNotes = string.Empty;
    public string EditNotes { get => _editNotes; set => SetField(ref _editNotes, value); }

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

    public CompaniesViewModel(ICompanyRepository repo, IDialogService dialog)
    {
        _repo = repo;
        _dialog = dialog;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        NewCommand = new RelayCommand(OpenNewForm);
        EditCommand = new RelayCommand(OpenEditForm, () => HasSelection);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !string.IsNullOrWhiteSpace(EditName));
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => HasSelection);
        CancelCommand = new RelayCommand(() => IsFormVisible = false);
    }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var all = await _repo.GetAllAsync();
            Companies.Clear();
            foreach (var c in all)
                Companies.Add(new CompanyDto(c.Id, c.Name, c.Website, c.Industry, c.Location));
            ApplyFilter();
            StatusMessage = $"{Companies.Count} companies";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredCompanies.Clear();
        var query = Companies.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var t = SearchText.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(t) ||
                (c.Industry?.ToLower().Contains(t) ?? false) ||
                (c.Location?.ToLower().Contains(t) ?? false));
        }
        foreach (var c in query.OrderBy(c => c.Name))
            FilteredCompanies.Add(c);
    }

    private void OpenNewForm()
    {
        _isEditing = false;
        EditName = EditWebsite = EditIndustry = EditLocation = EditNotes = string.Empty;
        IsFormVisible = true;
    }

    private void OpenEditForm()
    {
        if (_selectedCompany is null) return;
        _isEditing = true;
        EditName = _selectedCompany.Name;
        EditWebsite = _selectedCompany.Website ?? string.Empty;
        EditIndustry = _selectedCompany.Industry ?? string.Empty;
        EditLocation = _selectedCompany.Location ?? string.Empty;
        EditNotes = string.Empty;
        IsFormVisible = true;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (_isEditing && _selectedCompany is not null)
            {
                var entity = new Company
                {
                    Id = _selectedCompany.Id,
                    Name = EditName.Trim(),
                    Website = string.IsNullOrWhiteSpace(EditWebsite) ? null : EditWebsite.Trim(),
                    Industry = string.IsNullOrWhiteSpace(EditIndustry) ? null : EditIndustry.Trim(),
                    Location = string.IsNullOrWhiteSpace(EditLocation) ? null : EditLocation.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
                };
                await _repo.UpdateAsync(entity);
                StatusMessage = "Company updated.";
            }
            else
            {
                var entity = new Company
                {
                    Name = EditName.Trim(),
                    Website = string.IsNullOrWhiteSpace(EditWebsite) ? null : EditWebsite.Trim(),
                    Industry = string.IsNullOrWhiteSpace(EditIndustry) ? null : EditIndustry.Trim(),
                    Location = string.IsNullOrWhiteSpace(EditLocation) ? null : EditLocation.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
                };
                await _repo.AddAsync(entity);
                StatusMessage = "Company added.";
            }
            IsFormVisible = false;
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync()
    {
        if (_selectedCompany is null) return;
        if (!_dialog.Confirm("Delete Company",
            $"Delete \"{_selectedCompany.Name}\"?\n\nAll contacts linked to this company will also be removed."))
            return;

        IsBusy = true;
        try
        {
            await _repo.DeleteAsync(_selectedCompany.Id);
            StatusMessage = "Company deleted.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Delete failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
