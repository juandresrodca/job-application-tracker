using System.Collections.ObjectModel;
using JobTracker.Application.DTOs;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.Services;

namespace JobTracker.WPF.ViewModels;

public class SkillsViewModel : ViewModelBase, IRefreshable
{
    private readonly ISkillRepository _repo;
    private readonly IDialogService _dialog;

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<SkillDto> Skills { get; } = new();
    public ObservableCollection<SkillDto> FilteredSkills { get; } = new();

    // ── Selection / form ─────────────────────────────────────────────────────
    private SkillDto? _selectedSkill;
    public SkillDto? SelectedSkill
    {
        get => _selectedSkill;
        set { SetField(ref _selectedSkill, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selectedSkill is not null;

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

    private string _editCategory = string.Empty;
    public string EditCategory { get => _editCategory; set => SetField(ref _editCategory, value); }

    // ── Search ───────────────────────────────────────────────────────────────
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { SetField(ref _searchText, value); ApplyFilter(); }
    }

    // ── Existing categories for autocomplete ─────────────────────────────────
    public ObservableCollection<string> ExistingCategories { get; } = new();

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

    public SkillsViewModel(ISkillRepository repo, IDialogService dialog)
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
            Skills.Clear();
            ExistingCategories.Clear();
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in all)
            {
                Skills.Add(new SkillDto(s.Id, s.Name, s.Category));
                if (!string.IsNullOrWhiteSpace(s.Category))
                    categories.Add(s.Category);
            }
            foreach (var cat in categories.OrderBy(c => c))
                ExistingCategories.Add(cat);
            ApplyFilter();
            StatusMessage = $"{Skills.Count} skills";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredSkills.Clear();
        var query = Skills.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var t = SearchText.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(t) ||
                (s.Category?.ToLower().Contains(t) ?? false));
        }
        foreach (var s in query.OrderBy(s => s.Category).ThenBy(s => s.Name))
            FilteredSkills.Add(s);
    }

    private void OpenNewForm()
    {
        _isEditing = false;
        EditName = EditCategory = string.Empty;
        IsFormVisible = true;
    }

    private void OpenEditForm()
    {
        if (_selectedSkill is null) return;
        _isEditing = true;
        EditName = _selectedSkill.Name;
        EditCategory = _selectedSkill.Category ?? string.Empty;
        IsFormVisible = true;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (_isEditing && _selectedSkill is not null)
            {
                var entity = new Skill
                {
                    Id = _selectedSkill.Id,
                    Name = EditName.Trim(),
                    Category = string.IsNullOrWhiteSpace(EditCategory) ? null : EditCategory.Trim(),
                };
                await _repo.UpdateAsync(entity);
                StatusMessage = "Skill updated.";
            }
            else
            {
                var entity = new Skill
                {
                    Name = EditName.Trim(),
                    Category = string.IsNullOrWhiteSpace(EditCategory) ? null : EditCategory.Trim(),
                };
                await _repo.AddAsync(entity);
                StatusMessage = "Skill added.";
            }
            IsFormVisible = false;
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync()
    {
        if (_selectedSkill is null) return;
        if (!_dialog.Confirm("Delete Skill",
            $"Delete \"{_selectedSkill.Name}\"?\n\nThis will remove it from all job applications."))
            return;

        IsBusy = true;
        try
        {
            await _repo.DeleteAsync(_selectedSkill.Id);
            StatusMessage = "Skill deleted.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Delete failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
