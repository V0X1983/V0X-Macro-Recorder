using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class WindowCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<WindowAction>> ActionChoices { get; } =
    [
        new(WindowAction.Activate, "Activer"),
        new(WindowAction.Minimize, "Réduire"),
        new(WindowAction.Maximize, "Agrandir"),
        new(WindowAction.Restore, "Restaurer"),
        new(WindowAction.Close, "Fermer"),
        new(WindowAction.MoveResize, "Déplacer/redimensionner"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMoveResize))]
    private WindowAction _action;

    [ObservableProperty]
    private string? _windowTitle;

    [ObservableProperty]
    private string? _windowClassName;

    [ObservableProperty]
    private string _xText;

    [ObservableProperty]
    private string _yText;

    [ObservableProperty]
    private string _widthText;

    [ObservableProperty]
    private string _heightText;

    public WindowCommandEditorViewModel(WindowCommand? existing)
        : base(existing, "Fenêtre")
    {
        var command = existing ?? new WindowCommand();
        _action = command.Action;
        _windowTitle = command.WindowTitle;
        _windowClassName = command.WindowClassName;
        _xText = command.X.ToString(CultureInfo.InvariantCulture);
        _yText = command.Y.ToString(CultureInfo.InvariantCulture);
        _widthText = command.Width.ToString(CultureInfo.InvariantCulture);
        _heightText = command.Height.ToString(CultureInfo.InvariantCulture);
    }

    public bool ShowsMoveResize => Action == WindowAction.MoveResize;

    public override MacroCommand Build() => new WindowCommand
    {
        Action = Action,
        WindowTitle = WindowTitle,
        WindowClassName = WindowClassName,
        X = IntOr(XText, 0),
        Y = IntOr(YText, 0),
        Width = IntOr(WidthText, 800),
        Height = IntOr(HeightText, 600),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        (!string.IsNullOrWhiteSpace(WindowTitle) || !string.IsNullOrWhiteSpace(WindowClassName))
        && (!ShowsMoveResize || (TryInt(XText, out _) && TryInt(YText, out _) && TryNonNegative(WidthText, out _) && TryNonNegative(HeightText, out _)));
}
