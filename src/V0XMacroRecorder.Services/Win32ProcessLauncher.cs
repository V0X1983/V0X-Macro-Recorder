using System.Diagnostics;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

public sealed class Win32ProcessLauncher : IProcessLauncher
{
    public async Task<int?> LaunchAsync(string path, string? arguments, string? workingDirectory, bool shellExecute, bool waitForExit, int timeoutMs, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments ?? "",
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? "" : workingDirectory,
            UseShellExecute = shellExecute,
        };

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Impossible de démarrer « {path} ».");
        if (!waitForExit)
        {
            return null;
        }

        using var timeoutCts = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // Délai d'attente dépassé (pas une annulation utilisateur) : le processus continue en arrière-plan.
        }
    }
}
