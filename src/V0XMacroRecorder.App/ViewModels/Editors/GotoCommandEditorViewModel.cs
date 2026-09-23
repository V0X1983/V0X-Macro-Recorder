using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class GotoCommandEditorViewModel : CommandEditorViewModel
{
    public IReadOnlyList<string> KnownLabels { get; }

    [ObservableProperty]
    private string? _targetLabel;

    public GotoCommandEditorViewModel(GotoCommand? existing, IReadOnlyList<string> knownLabels)
        : base(existing, "Aller à l'étiquette")
    {
        KnownLabels = knownLabels;
        _targetLabel = (existing ?? new GotoCommand()).TargetLabel;
    }

    public override MacroCommand Build() => new GotoCommand { TargetLabel = TargetLabel ?? "", DelayMs = Delay };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(TargetLabel);
}
