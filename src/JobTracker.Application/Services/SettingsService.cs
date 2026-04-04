using System.Text.Json;
using JobTracker.Application.Interfaces;

namespace JobTracker.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private SettingsData _data = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "JobTracker");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Load();
    }

    public string? ObsidianVaultPath
    {
        get => _data.ObsidianVaultPath;
        set => _data.ObsidianVaultPath = value;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
        }
        catch
        {
            _data = new SettingsData();
        }
    }

    private class SettingsData
    {
        public string? ObsidianVaultPath { get; set; }
    }
}
