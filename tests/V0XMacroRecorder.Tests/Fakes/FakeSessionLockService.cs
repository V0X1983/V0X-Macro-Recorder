using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeSessionLockService : ISessionLockService
{
    public bool Locked { get; set; }

    public bool IsInputSessionLocked() => Locked;
}
