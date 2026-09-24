using System.Text.Json.Serialization;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Une étape d'une macro. Les sous-classes sont sérialisées avec un discriminateur "type" ; pour ajouter
/// un type de commande, déclarer ici son <see cref="JsonDerivedTypeAttribute"/> (le nom ne doit plus jamais changer).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MouseCommand), "mouse")]
[JsonDerivedType(typeof(KeyboardCommand), "keyboard")]
[JsonDerivedType(typeof(TextCommand), "text")]
[JsonDerivedType(typeof(WaitCommand), "wait")]
[JsonDerivedType(typeof(CommentCommand), "comment")]
[JsonDerivedType(typeof(ClipboardCommand), "clipboard")]
[JsonDerivedType(typeof(LaunchCommand), "program")]
[JsonDerivedType(typeof(OpenUrlCommand), "url")]
[JsonDerivedType(typeof(WindowCommand), "window")]
[JsonDerivedType(typeof(PixelCommand), "pixel")]
[JsonDerivedType(typeof(ImageSearchCommand), "image")]
[JsonDerivedType(typeof(SoundCommand), "sound")]
[JsonDerivedType(typeof(MessageCommand), "message")]
[JsonDerivedType(typeof(IfCommand), "if")]
[JsonDerivedType(typeof(ElseCommand), "else")]
[JsonDerivedType(typeof(EndIfCommand), "endif")]
[JsonDerivedType(typeof(LoopCommand), "loop")]
[JsonDerivedType(typeof(EndLoopCommand), "endloop")]
[JsonDerivedType(typeof(LabelCommand), "label")]
[JsonDerivedType(typeof(VariableCommand), "variable")]
[JsonDerivedType(typeof(GotoCommand), "goto")]
[JsonDerivedType(typeof(StopCommand), "stop")]
[JsonDerivedType(typeof(CallCommand), "call")]
[JsonDerivedType(typeof(PauseCommand), "pause")]
[JsonDerivedType(typeof(ScriptCommand), "script")]
[JsonDerivedType(typeof(SecureInputCommand), "secureinput")]
public abstract class MacroCommand
{
    private int _delayMs;

    /// <summary>Délai (ms) écoulé avant l'exécution de la commande.</summary>
    public int DelayMs
    {
        get => _delayMs;
        set => _delayMs = Math.Max(0, value);
    }

    /// <summary>Identifiant du type de commande (clé de la barre d'icônes).</summary>
    [JsonIgnore]
    public abstract string Kind { get; }

    /// <summary>Copie indépendante (tous les membres sont des valeurs ou des chaînes immuables).</summary>
    public MacroCommand Clone() => (MacroCommand)MemberwiseClone();
}

public sealed class MouseCommand : MacroCommand
{
    public override string Kind => "mouse";

    public MouseAction Action { get; set; } = MouseAction.Click;

    public MouseButton Button { get; set; } = MouseButton.Left;

    public int X { get; set; }

    public int Y { get; set; }

    public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Screen;

    /// <summary>Nombre de crans de molette : positif vers le haut, négatif vers le bas.</summary>
    public int WheelDelta { get; set; } = 1;

    /// <summary>Titre de la fenêtre visée quand <see cref="CoordinateMode"/> vaut ActiveWindow (recherchée à la lecture, étape 3).</summary>
    public string? WindowTitle { get; set; }

    /// <summary>Classe Win32 de la fenêtre visée (complète <see cref="WindowTitle"/> pour retrouver la fenêtre).</summary>
    public string? WindowClassName { get; set; }
}

public sealed class KeyboardCommand : MacroCommand
{
    private int _virtualKey = 0x41;
    private int _scanCode;

    public override string Kind => "keyboard";

    /// <summary>Code de touche virtuelle Windows (VK_*), entre 1 et 254.</summary>
    public int VirtualKey
    {
        get => _virtualKey;
        set => _virtualKey = Math.Clamp(value, 1, 254);
    }

    /// <summary>
    /// Code de balayage matériel (scan code), indépendant de la disposition du clavier (AZERTY/QWERTY…) :
    /// utilisé à la lecture (étape 3) pour une injection fidèle. 0 si inconnu (commande créée manuellement).
    /// </summary>
    public int ScanCode
    {
        get => _scanCode;
        set => _scanCode = Math.Max(0, value);
    }

    /// <summary>Vrai pour les touches étendues (bloc navigation, pavé num. /, touches Windows/menu…).</summary>
    public bool IsExtendedKey { get; set; }

    public KeyAction Action { get; set; } = KeyAction.Press;

    public KeyModifiers Modifiers { get; set; } = KeyModifiers.None;
}

public sealed class TextCommand : MacroCommand
{
    private int _characterDelayMs;

    public override string Kind => "text";

    public string Text { get; set; } = "";

    /// <summary>Délai (ms) entre deux caractères saisis.</summary>
    public int CharacterDelayMs
    {
        get => _characterDelayMs;
        set => _characterDelayMs = Math.Max(0, value);
    }

    /// <summary>Si vrai (par défaut), les jetons {date}/{clipboard}/{var:nom} sont remplacés avant la saisie.</summary>
    public bool ExpandTokens { get; set; } = true;
}

public enum ClipboardAction
{
    Copy,
    Paste,
    ReadToVariable,
}

public sealed class ClipboardCommand : MacroCommand
{
    public override string Kind => "clipboard";

    public ClipboardAction Action { get; set; } = ClipboardAction.Copy;

    /// <summary>Texte à copier (mode Copy) ; jetons {date}/{clipboard}/{var:nom} expansés à la lecture.</summary>
    public string Text { get; set; } = "";

    /// <summary>Variable recevant le contenu du presse-papiers (mode ReadToVariable).</summary>
    public string? VariableName { get; set; }
}

public enum LaunchMode
{
    Program,
    ShellCommand,
}

public sealed class LaunchCommand : MacroCommand
{
    private int _waitTimeoutMs = 30000;

    public override string Kind => "program";

    public LaunchMode Mode { get; set; } = LaunchMode.Program;

    public string Path { get; set; } = "";

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public bool WaitForExit { get; set; }

    public int WaitTimeoutMs
    {
        get => _waitTimeoutMs;
        set => _waitTimeoutMs = Math.Max(0, value);
    }
}

/// <summary>Ouvre une URL ou un fichier avec l'application associée (équivalent d'un double-clic dans l'explorateur).</summary>
public sealed class OpenUrlCommand : MacroCommand
{
    public override string Kind => "url";

    public string Path { get; set; } = "";
}

public enum WindowAction
{
    Activate,
    Minimize,
    Maximize,
    Restore,
    Close,
    MoveResize,
}

public sealed class WindowCommand : MacroCommand
{
    public override string Kind => "window";

    public WindowAction Action { get; set; } = WindowAction.Activate;

    /// <summary>Titre de la fenêtre visée (partiel, comme le repère « Fenêtre active » des commandes Souris).</summary>
    public string? WindowTitle { get; set; }

    public string? WindowClassName { get; set; }

    // MoveResize uniquement
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = 800;

    public int Height { get; set; } = 600;
}

/// <summary>Comportement d'une <see cref="WaitCommand"/> : une seule icône « Attente », plusieurs natures d'attente.</summary>
public enum WaitMode
{
    /// <summary>Délai fixe ou aléatoire (comportement historique).</summary>
    FixedDelay,
    WindowAppears,
    WindowDisappears,
    PixelMatch,
    ImageFound,
    KeyPress,
}

public sealed class WaitCommand : MacroCommand
{
    private int _durationMs = 1000;
    private int _randomExtraMs;
    private int _timeoutMs = 10000;

    public override string Kind => "wait";

    public WaitMode Mode { get; set; } = WaitMode.FixedDelay;

    /// <summary>Durée d'attente minimale (ms), mode FixedDelay.</summary>
    public int DurationMs
    {
        get => _durationMs;
        set => _durationMs = Math.Max(0, value);
    }

    /// <summary>Durée aléatoire ajoutée (0 = attente fixe) : l'attente réelle est comprise entre DurationMs et DurationMs + RandomExtraMs (mode FixedDelay).</summary>
    public int RandomExtraMs
    {
        get => _randomExtraMs;
        set => _randomExtraMs = Math.Max(0, value);
    }

    /// <summary>Délai maximal d'attente (ms) avant d'abandonner, pour tous les modes sauf FixedDelay.</summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(0, value);
    }

    // WindowAppears / WindowDisappears
    public string? WindowTitle { get; set; }

    public string? WindowClassName { get; set; }

    // KeyPress
    /// <summary>Code de touche virtuelle attendu, ou 0 pour n'importe quelle touche.</summary>
    public int VirtualKey { get; set; }

    // PixelMatch
    public int PixelX { get; set; }

    public int PixelY { get; set; }

    public string PixelColorHex { get; set; } = "#000000";

    private int _pixelTolerancePercent;

    public int PixelTolerancePercent
    {
        get => _pixelTolerancePercent;
        set => _pixelTolerancePercent = Math.Clamp(value, 0, 100);
    }

    // ImageFound
    public string? ImageTemplatePngBase64 { get; set; }

    public RectRegion? ImageSearchRegion { get; set; }

    private int _imageTolerancePercent = 10;

    public int ImageTolerancePercent
    {
        get => _imageTolerancePercent;
        set => _imageTolerancePercent = Math.Clamp(value, 0, 100);
    }
}

public enum PixelActionMode
{
    Wait,
    Test,
}

public sealed class PixelCommand : MacroCommand
{
    private int _timeoutMs = 10000;
    private int _tolerancePercent;

    public override string Kind => "pixel";

    public PixelActionMode Mode { get; set; } = PixelActionMode.Test;

    public int X { get; set; }

    public int Y { get; set; }

    public string ExpectedColorHex { get; set; } = "#000000";

    public int TolerancePercent
    {
        get => _tolerancePercent;
        set => _tolerancePercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>Délai maximal d'attente (ms), mode Wait.</summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(0, value);
    }

    /// <summary>Variable recevant "1"/"0" selon la correspondance (mode Test uniquement).</summary>
    public string? VariableName { get; set; }
}

public sealed class ImageSearchCommand : MacroCommand
{
    private int _tolerancePercent = 10;
    private int _timeoutMs = 10000;

    public override string Kind => "image";

    /// <summary>Image modèle, encodée en PNG puis en base64 (capturée à l'écran par l'utilisateur).</summary>
    public string TemplatePngBase64 { get; set; } = "";

    public int TemplateWidth { get; set; }

    public int TemplateHeight { get; set; }

    public int TolerancePercent
    {
        get => _tolerancePercent;
        set => _tolerancePercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>Région de recherche ; null = écran virtuel entier.</summary>
    public RectRegion? SearchRegion { get; set; }

    public bool ClickIfFound { get; set; }

    public MouseButton ClickButton { get; set; } = MouseButton.Left;

    /// <summary>Double-clic plutôt qu'un simple clic (ex. lancer une icône, qui ne se contente pas d'un clic simple) ; ignoré si <see cref="ClickIfFound"/> est faux.</summary>
    public bool DoubleClick { get; set; }

    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(0, value);
    }

    public string? FoundXVariable { get; set; }

    public string? FoundYVariable { get; set; }
}

public sealed class SoundCommand : MacroCommand
{
    public override string Kind => "sound";

    public string FilePath { get; set; } = "";

    public bool WaitForCompletion { get; set; }
}

public enum MessageBoxKind
{
    Info,
    Warning,
    Error,
    OkCancel,
}

public sealed class MessageCommand : MacroCommand
{
    public override string Kind => "message";

    public string Title { get; set; } = "V0X Macro Recorder";

    /// <summary>Jetons {date}/{clipboard}/{var:nom} expansés avant affichage.</summary>
    public string Text { get; set; } = "";

    public MessageBoxKind MessageKind { get; set; } = MessageBoxKind.Info;

    /// <summary>Variable recevant "ok"/"cancel" (mode OkCancel uniquement).</summary>
    public string? ResultVariableName { get; set; }
}

public sealed class CommentCommand : MacroCommand
{
    public override string Kind => "comment";

    public string Text { get; set; } = "";
}

/// <summary>Nature d'une condition évaluée par <c>Si</c> ou une boucle <c>Tant que</c> (voir <see cref="ConditionEvaluator"/>).</summary>
public enum ConditionKind
{
    WindowExists,
    PixelMatches,
    ImageFound,
    VariableCompare,
    FileExists,
}

public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains,
}

/// <summary>Rectangle en coordonnées écran (repère de recherche d'image). Non bornée : peut être négative (écran à gauche/au-dessus du principal).</summary>
public sealed class RectRegion
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}

/// <summary>
/// Condition intégrée par valeur dans <see cref="IfCommand"/> et <see cref="LoopCommand"/> (mode Tant que).
/// Ce n'est pas une <see cref="MacroCommand"/> : c'est un champ, pas une étape de la macro.
/// </summary>
public sealed class ConditionSpec
{
    public ConditionKind Kind { get; set; } = ConditionKind.WindowExists;

    // WindowExists
    public string? WindowTitle { get; set; }

    public string? WindowClassName { get; set; }

    // PixelMatches
    public int PixelX { get; set; }

    public int PixelY { get; set; }

    public string PixelColorHex { get; set; } = "#000000";

    private int _pixelTolerancePercent;

    public int PixelTolerancePercent
    {
        get => _pixelTolerancePercent;
        set => _pixelTolerancePercent = Math.Clamp(value, 0, 100);
    }

    // ImageFound
    public string? ImageTemplatePngBase64 { get; set; }

    private int _imageTolerancePercent = 10;

    public int ImageTolerancePercent
    {
        get => _imageTolerancePercent;
        set => _imageTolerancePercent = Math.Clamp(value, 0, 100);
    }

    public RectRegion? SearchRegion { get; set; }

    // VariableCompare
    public string? VariableName { get; set; }

    public ComparisonOperator ComparisonOperator { get; set; } = ComparisonOperator.Equals;

    public string ComparisonValue { get; set; } = "";

    // FileExists
    public string? FilePath { get; set; }
}

/// <summary>Début d'un bloc « Si… Sinon… Fin si » ; voir <see cref="Macros.BlockMap"/> pour l'appariement avec <see cref="ElseCommand"/>/<see cref="EndIfCommand"/>.</summary>
public sealed class IfCommand : MacroCommand
{
    public override string Kind => "if";

    public ConditionSpec Condition { get; set; } = new();

    public bool Negate { get; set; }
}

/// <summary>Marqueur « Sinon » (optionnel, ajouté via le menu contextuel sur un <see cref="IfCommand"/>) : pas d'éditeur, pas de délai.</summary>
public sealed class ElseCommand : MacroCommand
{
    public override string Kind => "else";
}

/// <summary>Marqueur de fin de bloc <see cref="IfCommand"/> : pas d'éditeur, pas de délai.</summary>
public sealed class EndIfCommand : MacroCommand
{
    public override string Kind => "endif";
}

/// <summary>Nature d'une boucle (voir <see cref="LoopCommand"/>).</summary>
public enum LoopMode
{
    RepeatCount,
    While,
    ForEachLine,
}

/// <summary>Début d'un bloc « Boucle… Fin boucle » ; voir <see cref="Macros.BlockMap"/> pour l'appariement avec <see cref="EndLoopCommand"/>.</summary>
public sealed class LoopCommand : MacroCommand
{
    public override string Kind => "loop";

    public LoopMode Mode { get; set; } = LoopMode.RepeatCount;

    private int _repeatCount = 1;

    /// <summary>Nombre de répétitions (mode RepeatCount, au moins 1).</summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => _repeatCount = Math.Max(1, value);
    }

    /// <summary>Condition réévaluée à chaque passage (mode While).</summary>
    public ConditionSpec? WhileCondition { get; set; }

    /// <summary>Fichier texte à parcourir ligne par ligne (mode ForEachLine).</summary>
    public string? FilePath { get; set; }

    /// <summary>Variable recevant la ligne courante (mode ForEachLine).</summary>
    public string? LineVariableName { get; set; }
}

/// <summary>Marqueur de fin de bloc <see cref="LoopCommand"/> : pas d'éditeur, pas de délai.</summary>
public sealed class EndLoopCommand : MacroCommand
{
    public override string Kind => "endloop";
}

/// <summary>Étiquette nommée, cible d'un <c>GotoCommand</c> (Aller à). Pas de délai.</summary>
public sealed class LabelCommand : MacroCommand
{
    public override string Kind => "label";

    public string Name { get; set; } = "";
}

public enum VariableMode
{
    Set,
    Increment,
    Calculate,
}

public sealed class VariableCommand : MacroCommand
{
    public override string Kind => "variable";

    public VariableMode Mode { get; set; } = VariableMode.Set;

    public string Name { get; set; } = "";

    /// <summary>Mode Set : valeur (jetons expansés). Mode Increment : delta numérique (défaut « 1 »). Mode Calculate : expression arithmétique.</summary>
    public string Value { get; set; } = "";
}

/// <summary>Saute vers l'étiquette nommée (résolue via <see cref="Macros.BlockMap.Labels"/>).</summary>
public sealed class GotoCommand : MacroCommand
{
    public override string Kind => "goto";

    public string TargetLabel { get; set; } = "";
}

/// <summary>Arrête la lecture en cours (celle de la macro qui contient cette commande, voir <c>CallCommand</c>).</summary>
public sealed class StopCommand : MacroCommand
{
    public override string Kind => "stop";
}

/// <summary>Appelle une autre macro (sous-routine) : gardée par une profondeur maximale et une détection de cycle dans <c>MacroPlayer</c>.</summary>
public sealed class CallCommand : MacroCommand
{
    public override string Kind => "call";

    public string MacroFilePath { get; set; } = "";
}

/// <summary>Pause de lecture : attend une frappe clavier avant de continuer (même primitive que <see cref="WaitMode.KeyPress"/>).</summary>
public sealed class PauseCommand : MacroCommand
{
    private int _timeoutMs;

    public override string Kind => "pause";

    /// <summary>Code de touche virtuelle attendu, ou 0 pour n'importe quelle touche.</summary>
    public int VirtualKey { get; set; }

    /// <summary>Délai maximal d'attente (ms), 0 ou moins = attente infinie.</summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(0, value);
    }
}

/// <summary>
/// Script C# (étape 5), exécuté par un <c>IScriptRunner</c> (Roslyn scripting, voir Services) avec un objet
/// « globals » <c>ScriptGlobals</c> (Core/Playback) exposant une API limitée : Mouse/Keyboard/Window/Clipboard/Vars/Log.
/// Jamais exécuté à l'ouverture d'un fichier, seulement à la lecture (voir avertissement de confiance à l'ouverture).
/// </summary>
public sealed class ScriptCommand : MacroCommand
{
    private int _timeoutSeconds = 10;

    public override string Kind => "script";

    public string Code { get; set; } = "";

    /// <summary>Délai maximal d'exécution (secondes) avant d'abandonner le script.</summary>
    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => _timeoutSeconds = Math.Clamp(value, 1, 300);
    }
}

/// <summary>
/// Saisie masquée (mot de passe/secret), étape 8 : jamais en clair dans le fichier macro. Deux modes exclusifs —
/// <see cref="PromptAtPlayback"/> vrai (par défaut, le plus sûr : rien n'est jamais écrit sur disque) demande le
/// secret via une invite à chaque lecture ; faux utilise <see cref="ProtectedValueBase64"/>, chiffré par
/// <c>IDataProtector</c> (DPAPI, utilisateur+machine courants uniquement).
/// </summary>
public sealed class SecureInputCommand : MacroCommand
{
    private int _characterDelayMs;

    public override string Kind => "secureinput";

    public bool PromptAtPlayback { get; set; } = true;

    /// <summary>Secret chiffré (DPAPI) ; toujours null quand <see cref="PromptAtPlayback"/> est vrai.</summary>
    public string? ProtectedValueBase64 { get; set; }

    /// <summary>Libellé affiché par l'invite en mode <see cref="PromptAtPlayback"/> (ex. « Mot de passe du site X »).</summary>
    public string PromptLabel { get; set; } = "Mot de passe";

    public int CharacterDelayMs
    {
        get => _characterDelayMs;
        set => _characterDelayMs = Math.Max(0, value);
    }
}
