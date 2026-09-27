using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class OpenUrlCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    private string _path;

    public OpenUrlCommandEditorViewModel(OpenUrlCommand? existing)
        : base(existing, "Ouvrir une adresse web")
    {
        _path = (existing ?? new OpenUrlCommand()).Path;
    }

    public override MacroCommand Build() => new OpenUrlCommand { Path = Path, DelayMs = Delay };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(Path);
}
