using Microsoft.Extensions.Logging.Abstractions;
using V0XMacroRecorder.Core.Models;
using V0XMacroRecorder.Services;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Tests;

/// <summary>Les tests redirigent AppPaths vers un dossier temporaire : jamais le vrai %AppData%.</summary>
[Collection("AppPaths")]
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _originalRoot = AppPaths.RootFolder;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "V0XMacroRecorderTests-" + Guid.NewGuid().ToString("N"));

    public SettingsServiceTests()
    {
        AppPaths.RootFolder = _tempRoot;
    }

    public void Dispose()
    {
        AppPaths.RootFolder = _originalRoot;
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Defaults_to_dark_theme_when_no_config_exists()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance);

        Assert.Equal(AppSettings.DarkTheme, service.Current.Theme);
    }

    [Fact]
    public async Task Saved_theme_is_reloaded_by_a_new_instance()
    {
        var first = new SettingsService(NullLogger<SettingsService>.Instance);
        first.Current.Theme = AppSettings.LightTheme;
        await first.SaveAsync();

        var second = new SettingsService(NullLogger<SettingsService>.Instance);

        Assert.Equal(AppSettings.LightTheme, second.Current.Theme);
    }

    [Fact]
    public async Task SaveAsync_raises_SettingsChanged()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance);
        var raised = false;
        service.SettingsChanged += (_, _) => raised = true;

        await service.SaveAsync();

        Assert.True(raised);
    }

    [Fact]
    public void Corrupted_config_falls_back_to_defaults()
    {
        File.WriteAllText(AppPaths.ConfigFilePath, "{ ceci n'est pas du JSON");

        var service = new SettingsService(NullLogger<SettingsService>.Instance);

        Assert.Equal(AppSettings.DarkTheme, service.Current.Theme);
    }
}
