using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>Élévation UAC à la demande (pur .NET, aucun P/Invoke nécessaire).</summary>
public sealed class Win32ElevationService : IElevationService
{
    private readonly ILogger<Win32ElevationService> _logger;

    public Win32ElevationService(ILogger<Win32ElevationService> logger)
    {
        _logger = logger;
    }

    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool RelaunchElevated()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            using var process = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" });
            return process is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // L'utilisateur a refusé l'invite UAC, ou une autre erreur de lancement.
            _logger.LogWarning(ex, "Relance en administrateur annulée ou échouée.");
            return false;
        }
    }
}
