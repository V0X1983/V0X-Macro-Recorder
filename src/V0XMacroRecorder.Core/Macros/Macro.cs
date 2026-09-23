namespace V0XMacroRecorder.Core.Macros;

/// <summary>Une macro : liste ordonnée de commandes et ses réglages de lecture. Sérialisée en JSON (fichier .v0xmacro).</summary>
public sealed class Macro
{
    public const string FileExtension = ".v0xmacro";

    private double _playbackSpeed = 1.0;
    private int _repeatCount = 1;
    private List<MacroCommand> _commands = [];

    /// <summary>Version du format du fichier ; sert aux migrations (voir <see cref="MacroSerializer"/>).</summary>
    public int SchemaVersion { get; set; } = MacroSerializer.CurrentSchemaVersion;

    public string Name { get; set; } = "";

    /// <summary>Multiplicateur de vitesse de lecture (1 = vitesse enregistrée), entre 0,1 et 10.</summary>
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = double.IsFinite(value) ? Math.Clamp(value, 0.1, 10.0) : 1.0;
    }

    /// <summary>Nombre de répétitions de la macro (au moins 1).</summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => _repeatCount = Math.Max(1, value);
    }

    public List<MacroCommand> Commands
    {
        get => _commands;
        set => _commands = value ?? [];
    }

    /// <summary>
    /// Dimensions du bureau virtuel au moment du premier enregistrement (étape 8), 0 si inconnu (macro jamais
    /// enregistrée, ou créée avant l'étape 8) : sert uniquement à avertir d'un écart à la lecture
    /// (résolution/DPI changés, écran débranché), jamais à bloquer la lecture.
    /// </summary>
    public int RecordedScreenWidth { get; set; }

    public int RecordedScreenHeight { get; set; }
}
