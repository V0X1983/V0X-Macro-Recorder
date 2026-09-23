using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

public sealed partial class VariableCommandEditorViewModel : CommandEditorViewModel
{
    public static IReadOnlyList<Choice<VariableMode>> ModeChoices { get; } =
    [
        new(VariableMode.Set, "Définir"),
        new(VariableMode.Increment, "Incrémenter"),
        new(VariableMode.Calculate, "Calculer"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueHint))]
    private VariableMode _mode;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _value;

    public VariableCommandEditorViewModel(VariableCommand? existing)
        : base(existing, "Variable")
    {
        var command = existing ?? new VariableCommand();
        _mode = command.Mode;
        _name = command.Name;
        _value = command.Value;
    }

    public string ValueHint => Mode switch
    {
        VariableMode.Set => "Valeur (jetons {date}/{clipboard}/{var:nom} acceptés)",
        VariableMode.Increment => "Delta (nombre, défaut 1 si vide)",
        VariableMode.Calculate => "Expression (+ - * / et parenthèses, {var:nom} acceptés)",
        _ => "Valeur",
    };

    public override MacroCommand Build() => new VariableCommand { Mode = Mode, Name = Name, Value = Value, DelayMs = Delay };

    protected override bool AreFieldsValid() => !string.IsNullOrWhiteSpace(Name);
}
