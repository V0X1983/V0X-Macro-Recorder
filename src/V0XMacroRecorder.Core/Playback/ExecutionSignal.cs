namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Résultat de l'exécution d'une commande : soit continuer à la suivante, soit arrêter toute la lecture,
/// soit sauter à un index précis (contrôle de flux : Si/Sinon/Boucle/Aller à).
/// </summary>
public readonly struct ExecutionSignal
{
    public enum SignalKind { Continue, Stop, Jump }

    private ExecutionSignal(SignalKind signal, int targetIndex)
    {
        Signal = signal;
        TargetIndex = targetIndex;
    }

    public SignalKind Signal { get; }

    /// <summary>Index de destination ; n'a de sens que lorsque <see cref="Signal"/> vaut <see cref="SignalKind.Jump"/>.</summary>
    public int TargetIndex { get; }

    public static readonly ExecutionSignal Continue = new(SignalKind.Continue, 0);

    public static readonly ExecutionSignal Stop = new(SignalKind.Stop, 0);

    public static ExecutionSignal JumpTo(int index) => new(SignalKind.Jump, index);
}
