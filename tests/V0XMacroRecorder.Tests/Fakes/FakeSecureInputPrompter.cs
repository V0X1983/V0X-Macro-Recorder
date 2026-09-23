using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeSecureInputPrompter : ISecureInputPrompter
{
    public string? NextResult { get; set; }

    public List<(string Title, string Message)> Calls { get; } = [];

    public string? PromptForSecret(string title, string message)
    {
        Calls.Add((title, message));
        return NextResult;
    }
}
