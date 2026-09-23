using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeElevationService : IElevationService
{
    public bool IsElevated { get; set; }

    public bool RelaunchElevated() => true;
}
