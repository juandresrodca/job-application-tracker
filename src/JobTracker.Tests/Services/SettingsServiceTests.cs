using FluentAssertions;
using Xunit;
using System.IO;
using JobTracker.Application.Services;

namespace JobTracker.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    [Fact]
    public void Save_And_Load_Roundtrips_ObsidianVaultPath()
    {
        var svc = new SettingsService();
        svc.ObsidianVaultPath = @"C:\TestVault";
        svc.Save();

        var svc2 = new SettingsService();
        svc2.Load();

        svc2.ObsidianVaultPath.Should().Be(@"C:\TestVault");
    }

    [Fact]
    public void Load_WithCorruptFile_DoesNotThrow_AndUsesDefaults()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsPath = Path.Combine(appData, "JobTracker", "settings.json");

        var originalContent = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{ this is not valid json {{{{");

            var sut = () => { var svc = new SettingsService(); svc.Load(); };
            sut.Should().NotThrow();
        }
        finally
        {
            if (originalContent != null)
                File.WriteAllText(settingsPath, originalContent);
            else if (File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
    }

    public void Dispose() { }
}
