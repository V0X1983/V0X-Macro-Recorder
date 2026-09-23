using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.ViewModels.Editors;

/// <summary>Une valeur d'énumération et son libellé français, pour les listes déroulantes.</summary>
public sealed record Choice<T>(T Value, string Label);

/// <summary>
/// Base des boîtes de dialogue d'édition d'une commande. L'éditeur travaille sur des copies des valeurs
/// (champs texte pour les nombres, afin de signaler une saisie invalide) ; <see cref="Build"/> produit la commande.
/// </summary>
public abstract class CommandEditorViewModel : ObservableObject
{
    /// <summary>Types de commande éditables : à étendre quand une étape de la roadmap ajoute un type.</summary>
    public static readonly IReadOnlySet<string> SupportedKinds =
        new HashSet<string> { "mouse", "keyboard", "wait", "text", "comment", "clipboard", "program", "url", "window", "pixel", "sound", "message", "image", "if", "loop", "variable", "label", "goto", "stop", "call", "pause", "script", "secureinput" };

    private readonly int _preservedDelayMs;
    private string _delayText;

    protected CommandEditorViewModel(MacroCommand? existing, string kindLabel)
    {
        _preservedDelayMs = existing?.DelayMs ?? 0;
        _delayText = _preservedDelayMs.ToString(CultureInfo.InvariantCulture);
        Title = $"{(existing is null ? "Ajouter" : "Modifier")} : {kindLabel}";
    }

    public string Title { get; }

    /// <summary>Faux pour les commandes qui portent leur propre durée (Attente) ou ne s'exécutent pas (Commentaire).</summary>
    public virtual bool ShowsDelay => true;

    /// <summary>Délai (ms) avant l'exécution de la commande.</summary>
    public string DelayText
    {
        get => _delayText;
        set => SetProperty(ref _delayText, value);
    }

    public bool IsValid => (!ShowsDelay || TryNonNegative(DelayText, out _)) && AreFieldsValid();

    protected int Delay => ShowsDelay && TryNonNegative(DelayText, out var delay) ? delay : _preservedDelayMs;

    public static CommandEditorViewModel Create(string kind, MacroCommand? existing, Infrastructure.IDialogService? dialogs = null, IReadOnlyList<string>? knownLabels = null, IDataProtector? dataProtector = null) => kind switch
    {
        "mouse" => new MouseCommandEditorViewModel(existing as MouseCommand),
        "keyboard" => new KeyboardCommandEditorViewModel(existing as KeyboardCommand),
        "text" => new TextCommandEditorViewModel(existing as TextCommand),
        "wait" => new WaitCommandEditorViewModel(existing as WaitCommand, dialogs),
        "comment" => new CommentCommandEditorViewModel(existing as CommentCommand),
        "clipboard" => new ClipboardCommandEditorViewModel(existing as ClipboardCommand),
        "program" => new LaunchCommandEditorViewModel(existing as LaunchCommand),
        "url" => new OpenUrlCommandEditorViewModel(existing as OpenUrlCommand),
        "window" => new WindowCommandEditorViewModel(existing as WindowCommand),
        "pixel" => new PixelCommandEditorViewModel(existing as PixelCommand, dialogs),
        "sound" => new SoundCommandEditorViewModel(existing as SoundCommand),
        "message" => new MessageCommandEditorViewModel(existing as MessageCommand),
        "image" => new ImageSearchCommandEditorViewModel(existing as ImageSearchCommand, dialogs),
        "if" => new IfCommandEditorViewModel(existing as IfCommand, dialogs),
        "loop" => new LoopCommandEditorViewModel(existing as LoopCommand, dialogs),
        "variable" => new VariableCommandEditorViewModel(existing as VariableCommand),
        "label" => new LabelCommandEditorViewModel(existing as LabelCommand),
        "goto" => new GotoCommandEditorViewModel(existing as GotoCommand, knownLabels ?? []),
        "stop" => new StopCommandEditorViewModel(existing as StopCommand),
        "call" => new CallCommandEditorViewModel(existing as CallCommand, dialogs),
        "pause" => new PauseCommandEditorViewModel(existing as PauseCommand),
        "script" => new ScriptCommandEditorViewModel(existing as ScriptCommand, dialogs),
        "secureinput" => new SecureInputCommandEditorViewModel(existing as SecureInputCommand, dataProtector),
        _ => throw new ArgumentException($"Type de commande non éditable : {kind}", nameof(kind)),
    };

    public abstract MacroCommand Build();

    protected abstract bool AreFieldsValid();

    protected static bool TryInt(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    protected static bool TryNonNegative(string? text, out int value) => TryInt(text, out value) && value >= 0;

    protected static int IntOr(string? text, int fallback) => TryInt(text, out var value) ? value : fallback;

    /// <summary>Toute modification d'un champ peut changer la validité du formulaire.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(IsValid))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsValid)));
        }
    }
}
