using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class KeyboardCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<KeyAction>> ActionChoices { get; } =
    [
        new(KeyAction.Press, "Appuyer puis relâcher"),
        new(KeyAction.Down, "Maintenir enfoncée"),
        new(KeyAction.Up, "Relâcher"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private int _virtualKey;

    [ObservableProperty]
    private KeyAction _action;

    [ObservableProperty]
    private bool _ctrl;

    [ObservableProperty]
    private bool _alt;

    [ObservableProperty]
    private bool _shift;

    [ObservableProperty]
    private bool _win;

    public KeyboardCommandEditorViewModel(KeyboardCommand? existing)
        : base(existing, "Clavier")
    {
        var command = existing ?? new KeyboardCommand();
        _virtualKey = command.VirtualKey;
        _action = command.Action;
        _ctrl = command.Modifiers.HasFlag(KeyModifiers.Ctrl);
        _alt = command.Modifiers.HasFlag(KeyModifiers.Alt);
        _shift = command.Modifiers.HasFlag(KeyModifiers.Shift);
        _win = command.Modifiers.HasFlag(KeyModifiers.Win);
    }

    public string KeyLabel => VirtualKeyNames.GetName(VirtualKey);

    public override MacroCommand Build() => new KeyboardCommand
    {
        VirtualKey = VirtualKey,
        Action = Action,
        Modifiers = (Ctrl ? KeyModifiers.Ctrl : 0)
                    | (Alt ? KeyModifiers.Alt : 0)
                    | (Shift ? KeyModifiers.Shift : 0)
                    | (Win ? KeyModifiers.Win : 0),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => VirtualKey is >= 1 and <= 254;
}
