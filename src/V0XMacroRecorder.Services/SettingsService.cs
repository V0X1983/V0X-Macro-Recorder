using System.Text.Json;
using Microsoft.Extensions.Logging;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Models;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Charge/sauvegarde les paramètres de l'application dans %AppData%\V0XMacroRecorder\config.json.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Current = Load();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(AppPaths.ConfigFilePath, json, cancellationToken);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Échec de la sauvegarde des paramètres.");
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigFilePath))
            {
                var json = File.ReadAllText(AppPaths.ConfigFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fichier de config corrompu ou illisible : on repart sur des valeurs par défaut.
        }

        return new AppSettings();
    }
}
