using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class PauseCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private int _virtualKey;

    [ObservableProperty]
    private string _timeoutText;

    public PauseCommandEditorViewModel(PauseCommand? existing)
        : base(existing, "Pause (attendre une touche)")
    {
        var command = existing ?? new PauseCommand();
        _virtualKey = command.VirtualKey;
        _timeoutText = command.TimeoutMs.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>0 (n'importe quelle touche) n'est pas dans <see cref="VirtualKeyNames"/> : libellé dédié.</summary>
    public string KeyLabel => VirtualKey == 0 ? "N'importe quelle touche" : VirtualKeyNames.GetName(VirtualKey);

    public override MacroCommand Build() => new PauseCommand
    {
        VirtualKey = VirtualKey,
        TimeoutMs = IntOr(TimeoutText, 0),
        DelayMs = Delay,
    };

    protected override bool AreFieldsValid() => TryNonNegative(TimeoutText, out _);
}
