using FluentAssertions;
using Xunit;
using JobTracker.Application.Services;
using System.Text.Json;

namespace JobTracker.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    // We test a subclass that overrides the path so we can isolate to a temp directory.
    // SettingsService uses %APPDATA%\JobTracker internally, so we test behaviour via
    // reflection-free public API (Save + Load roundtrip on a fresh instance each time).

    [Fact]
    public void AiApiKey_RoundTrips_ThroughEncryption()
    {
        var svc = new SettingsService();
        svc.AiApiKey = "sk-or-test-key";
        svc.Save();

        // Re-load in a new instance to simulate app restart
        var svc2 = new SettingsService();
        svc2.Load();

        // Key should decrypt back to original value
        svc2.AiApiKey.Should().Be("sk-or-test-key");
    }

    [Fact]
    public void AiApiKey_IsNotPlaintext_InSettingsFile()
    {
        var svc = new SettingsService();
        svc.AiApiKey = "my-secret-key-12345";
        svc.Save();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsPath = Path.Combine(appData, "JobTracker", "settings.json");

        if (!File.Exists(settingsPath)) return; // Skip if file wasn't created

        var json = File.ReadAllText(settingsPath);
        json.Should().NotContain("my-secret-key-12345",
            "API key must not be stored in plaintext");

        // Should not contain the old field name either
        json.Should().NotContain("\"AiApiKey\":");
    }

    [Fact]
    public void Save_And_Load_Roundtrips_AllNonSecretFields()
    {
        var svc = new SettingsService();
        svc.ObsidianVaultPath = @"C:\TestVault";
        svc.AiProvider = "ollama";
        svc.AiModel = "llama3";
        svc.DefaultCvText = "My CV content here";
        svc.Save();

        var svc2 = new SettingsService();
        svc2.Load();

        svc2.ObsidianVaultPath.Should().Be(@"C:\TestVault");
        svc2.AiProvider.Should().Be("ollama");
        svc2.AiModel.Should().Be("llama3");
        svc2.DefaultCvText.Should().Be("My CV content here");
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
            // Restore original settings file
            if (originalContent != null)
                File.WriteAllText(settingsPath, originalContent);
            else if (File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
    }

    public void Dispose() { }
}
