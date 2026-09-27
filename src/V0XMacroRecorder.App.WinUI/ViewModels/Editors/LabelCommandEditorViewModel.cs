using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class LabelCommandEditorViewModel : CommandEditorViewModel
{
    [ObservableProperty]
    private string _name;

    public LabelCommandEditorViewModel(LabelCommand? existing)
        : base(existing, "Étiquette")
    {
        _name = (existing ?? new LabelCommand()).Name;
    }

    public override bool ShowsDelay => false;

    public override MacroCommand Build() => new LabelCommand { Name = Name };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(Name);
}
