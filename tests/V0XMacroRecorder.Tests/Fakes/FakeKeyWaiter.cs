using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeKeyWaiter : IKeyWaiter
{
    public sealed record Call(int VirtualKey, int TimeoutMs);

    public List<Call> Calls { get; } = [];

    /// <summary>Résultat renvoyé par le prochain appel (vrai = touche pressée, faux = délai dépassé).</summary>
    public bool Result { get; set; } = true;

    public Task<bool> WaitForKeyAsync(int virtualKey, int timeoutMs, CancellationToken ct)
    {
        Calls.Add(new Call(virtualKey, timeoutMs));
        return Task.FromResult(Result);
    }
}
