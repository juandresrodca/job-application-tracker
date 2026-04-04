using JobTracker.Application.Interfaces;

namespace JobTracker.WPF.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IMarkdownSyncService _markdownSync;

    public string ObsidianVaultPath
    {
        get => _settings.ObsidianVaultPath ?? string.Empty;
        set { _settings.ObsidianVaultPath = value; OnPropertyChanged(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand BrowseVaultCommand { get; }
    public AsyncRelayCommand SyncAllCommand { get; }
    public AsyncRelayCommand SyncAndCleanupCommand { get; }

    public SettingsViewModel(ISettingsService settings, IMarkdownSyncService markdownSync)
    {
        _settings = settings;
        _markdownSync = markdownSync;

        SaveCommand = new RelayCommand(() =>
        {
            _settings.Save();
            StatusMessage = "Settings saved.";
        });

        BrowseVaultCommand = new RelayCommand(BrowseVault);

        SyncAllCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Syncing all applications to Obsidian...";
            await _markdownSync.SyncAllAsync();
            StatusMessage = "Sync complete.";
        });

        SyncAndCleanupCommand = new AsyncRelayCommand(async () =>
        {
            StatusMessage = "Syncing and cleaning up orphaned files...";
            var result = await _markdownSync.SyncAndCleanupAsync();
            StatusMessage = result.Success
                ? $"✓ {result.ErrorMessage ?? "Sync and cleanup complete."}"
                : $"✗ {result.ErrorMessage ?? "Sync and cleanup failed."}";
        });
    }

    private void BrowseVault()
    {
        // WPF FolderBrowserDialog (requires WPF reference in .csproj)
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select your Obsidian Vault folder",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ObsidianVaultPath = dialog.FolderName;
            _markdownSync.VaultPath = dialog.FolderName;
        }
    }
}
