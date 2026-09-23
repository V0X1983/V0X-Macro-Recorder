using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeSoundPlayer : ISoundPlayer
{
    public sealed record Call(string FilePath, bool WaitForCompletion);

    public List<Call> Calls { get; } = [];

    public Exception? ThrowOnPlay { get; set; }

    public Task PlayAsync(string filePath, bool waitForCompletion, CancellationToken ct)
    {
        Calls.Add(new Call(filePath, waitForCompletion));
        if (ThrowOnPlay is not null)
        {
            throw ThrowOnPlay;
        }

        return Task.CompletedTask;
    }
}
