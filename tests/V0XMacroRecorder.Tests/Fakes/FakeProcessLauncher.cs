using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public sealed record Call(string Path, string? Arguments, string? WorkingDirectory, bool ShellExecute, bool WaitForExit, int TimeoutMs);

    public List<Call> Calls { get; } = [];

    /// <summary>Code de sortie renvoyé quand WaitForExit est demandé (null = délai dépassé).</summary>
    public int? ExitCode { get; set; } = 0;

    public Exception? ThrowOnLaunch { get; set; }

    public Task<int?> LaunchAsync(string path, string? arguments, string? workingDirectory, bool shellExecute, bool waitForExit, int timeoutMs, CancellationToken ct)
    {
        Calls.Add(new Call(path, arguments, workingDirectory, shellExecute, waitForExit, timeoutMs));
        if (ThrowOnLaunch is not null)
        {
            throw ThrowOnLaunch;
        }

        return Task.FromResult(waitForExit ? ExitCode : null);
    }
}
