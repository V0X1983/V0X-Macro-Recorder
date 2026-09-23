using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class ClipboardCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<ClipboardAction>> ActionChoices { get; } =
    [
        new(ClipboardAction.Copy, "Copier un texte"),
        new(ClipboardAction.Paste, "Coller (Ctrl+V)"),
        new(ClipboardAction.ReadToVariable, "Lire dans une variable"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsText), nameof(ShowsVariable))]
    private ClipboardAction _action;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private string? _variableName;

    public ClipboardCommandEditorViewModel(ClipboardCommand? existing)
        : base(existing, "Presse-papiers")
    {
        var command = existing ?? new ClipboardCommand();
        _action = command.Action;
        _text = command.Text;
        _variableName = command.VariableName;
    }

    public bool ShowsText => Action == ClipboardAction.Copy;

    public bool ShowsVariable => Action == ClipboardAction.ReadToVariable;

    public override MacroCommand Build() => new ClipboardCommand
    {
        Action = Action,
        Text = Text,
        VariableName = VariableName,
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => !ShowsVariable || !string.IsNullOrWhiteSpace(VariableName);
}
