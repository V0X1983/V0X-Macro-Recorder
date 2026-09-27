using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class MessageCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<MessageBoxKind>> KindChoices { get; } =
    [
        new(MessageBoxKind.Info, "Information"),
        new(MessageBoxKind.Warning, "Avertissement"),
        new(MessageBoxKind.Error, "Erreur"),
        new(MessageBoxKind.OkCancel, "OK/Annuler"),
    ];

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsResultVariable))]
    private MessageBoxKind _messageKind;

    [ObservableProperty]
    private string? _resultVariableName;

    public MessageCommandEditorViewModel(MessageCommand? existing)
        : base(existing, "Afficher un message")
    {
        var command = existing ?? new MessageCommand();
        _title = command.Title;
        _text = command.Text;
        _messageKind = command.MessageKind;
        _resultVariableName = command.ResultVariableName;
    }

    public bool ShowsResultVariable => MessageKind == MessageBoxKind.OkCancel;

    public override MacroCommand Build() => new MessageCommand
    {
        Title = Title,
        Text = Text,
        MessageKind = MessageKind,
        ResultVariableName = ResultVariableName,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(Title);
}
