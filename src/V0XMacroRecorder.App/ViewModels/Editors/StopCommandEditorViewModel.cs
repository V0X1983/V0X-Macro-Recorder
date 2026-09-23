using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed class StopCommandEditorViewModel : CommandEditorViewModel
{
    public StopCommandEditorViewModel(StopCommand? existing)
        : base(existing, "Arrêter la macro")
    {
    }

    public override MacroCommand Build() => new StopCommand { DelayMs = Delay };

    protected override bool AreFieldsValid() => true;
}
