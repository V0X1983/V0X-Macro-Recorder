namespace V0XMacroRecorder.Core.Playback;

/// <summary>Que faire quand une commande en repère « Fenêtre active » ne retrouve pas sa fenêtre.</summary>
public enum WindowNotFoundAction
{
    /// <summary>Ignorer cette commande et continuer avec la suivante.</summary>
    Ignore,

    /// <summary>Arrêter complètement la lecture.</summary>
    Stop,

    /// <summary>Attendre (en réessayant périodiquement) jusqu'à <see cref="PlaybackOptions.WaitForWindowTimeoutMs"/>, puis agir comme <see cref="Ignore"/>.</summary>
    WaitAndRetry,
}

/// <summary>Réglages de lecture, choisis dans le menu du bouton LECTURE et persistés dans AppSettings.</summary>
public sealed class PlaybackOptions
{
    private double _speedMultiplier = 1.0;
    private int _repeatCount = 1;
    private int _delayBetweenRepeatsMs;
    private int _waitForWindowTimeoutMs = 5000;

    /// <summary>Multiplicateur appliqué à tous les délais (0,1 à 10) ; ignoré si <see cref="NoDelay"/> est vrai.</summary>
    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => _speedMultiplier = double.IsFinite(value) ? Math.Clamp(value, 0.1, 10.0) : 1.0;
    }

    /// <summary>Lecture au plus vite : tous les délais (avant commande, Attente, entre caractères) deviennent 0.</summary>
    public bool NoDelay { get; set; }

    /// <summary>Nombre de répétitions de la macro (ignoré si <see cref="InfiniteLoop"/>).</summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => _repeatCount = Math.Max(1, value);
    }

    public bool InfiniteLoop { get; set; }

    public int DelayBetweenRepeatsMs
    {
        get => _delayBetweenRepeatsMs;
        set => _delayBetweenRepeatsMs = Math.Max(0, value);
    }

    public WindowNotFoundAction WindowNotFoundAction { get; set; } = WindowNotFoundAction.WaitAndRetry;

    public int WaitForWindowTimeoutMs
    {
        get => _waitForWindowTimeoutMs;
        set => _waitForWindowTimeoutMs = Math.Max(0, value);
    }

    /// <summary>Lecture pas à pas (façon F10) : la lecture se met en pause après chaque commande.</summary>
    public bool StepMode { get; set; }
}
