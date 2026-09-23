using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeMessageBoxService : IMessageBoxService
{
    public sealed record Call(string Title, string Text, MessageBoxKind Kind);

    public List<Call> Calls { get; } = [];

    public MessageBoxResult Result { get; set; } = MessageBoxResult.Ok;

    public MessageBoxResult Show(string title, string text, MessageBoxKind kind)
    {
        Calls.Add(new Call(title, text, kind));
        return Result;
    }
}
