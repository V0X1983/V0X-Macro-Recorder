using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class MouseCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<MouseAction>> ActionChoices { get; } =
    [
        new(MouseAction.Move, "Déplacer le curseur"),
        new(MouseAction.Click, "Clic"),
        new(MouseAction.DoubleClick, "Double-clic"),
        new(MouseAction.Down, "Bouton enfoncé"),
        new(MouseAction.Up, "Bouton relâché"),
        new(MouseAction.Wheel, "Molette"),
    ];

    public static IReadOnlyList<Choice<MouseButton>> ButtonChoices { get; } =
    [
        new(MouseButton.Left, "Gauche"),
        new(MouseButton.Right, "Droit"),
        new(MouseButton.Middle, "Milieu"),
    ];

    public static IReadOnlyList<Choice<CoordinateMode>> CoordinateModeChoices { get; } =
    [
        new(CoordinateMode.Screen, "Écran (coordonnées absolues)"),
        new(CoordinateMode.ActiveWindow, "Fenêtre active"),
        new(CoordinateMode.Relative, "Relatif à la position actuelle"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsButton), nameof(ShowsPosition), nameof(ShowsWheel))]
    private MouseAction _action;

    [ObservableProperty]
    private MouseButton _button;

    [ObservableProperty]
    private CoordinateMode _coordinateMode;

    [ObservableProperty]
    private string _xText;

    [ObservableProperty]
    private string _yText;

    [ObservableProperty]
    private string _wheelText;

    public MouseCommandEditorViewModel(MouseCommand? existing)
        : base(existing, "Souris")
    {
        var command = existing ?? new MouseCommand();
        _action = command.Action;
        _button = command.Button;
        _coordinateMode = command.CoordinateMode;
        _xText = command.X.ToString(CultureInfo.InvariantCulture);
        _yText = command.Y.ToString(CultureInfo.InvariantCulture);
        _wheelText = command.WheelDelta.ToString(CultureInfo.InvariantCulture);
    }

    public bool ShowsButton => Action is MouseAction.Click or MouseAction.DoubleClick or MouseAction.Down or MouseAction.Up;

    public bool ShowsPosition => Action != MouseAction.Wheel;

    public bool ShowsWheel => Action == MouseAction.Wheel;

    public override MacroCommand Build() => new MouseCommand
    {
        Action = Action,
        Button = Button,
        CoordinateMode = CoordinateMode,
        X = IntOr(XText, 0),
        Y = IntOr(YText, 0),
        WheelDelta = IntOr(WheelText, 1),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        (!ShowsPosition || (TryInt(XText, out _) && TryInt(YText, out _)))
        && (!ShowsWheel || (TryInt(WheelText, out var notches) && notches != 0));
}
