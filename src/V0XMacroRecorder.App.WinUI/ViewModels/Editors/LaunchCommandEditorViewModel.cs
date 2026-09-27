using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class LaunchCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<LaunchMode>> ModeChoices { get; } =
    [
        new(LaunchMode.Program, "Programme"),
        new(LaunchMode.ShellCommand, "Commande shell"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsProgramFields))]
    private LaunchMode _mode;

    [ObservableProperty]
    private string _path;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTimeout))]
    private bool _waitForExit;

    [ObservableProperty]
    private string _waitTimeoutText;

    public LaunchCommandEditorViewModel(LaunchCommand? existing)
        : base(existing, "Lancer un programme ou un fichier")
    {
        var command = existing ?? new LaunchCommand();
        _mode = command.Mode;
        _path = command.Path;
        _arguments = command.Arguments;
        _workingDirectory = command.WorkingDirectory;
        _waitForExit = command.WaitForExit;
        _waitTimeoutText = command.WaitTimeoutMs.ToString(CultureInfo.InvariantCulture);
    }

    public bool ShowsProgramFields => Mode == LaunchMode.Program;

    public bool ShowsTimeout => WaitForExit;

    public override MacroCommand Build() => new LaunchCommand
    {
        Mode = Mode,
        Path = Path,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        WaitForExit = WaitForExit,
        WaitTimeoutMs = IntOr(WaitTimeoutText, 30000),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() =>
        !string.IsNullOrWhiteSpace(Path) && (!ShowsTimeout || TryNonNegative(WaitTimeoutText, out _));
}
