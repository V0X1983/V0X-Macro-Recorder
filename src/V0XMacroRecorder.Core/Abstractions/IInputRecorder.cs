using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Enregistre les actions souris/clavier de l'utilisateur en commandes de macro (bouton ENREGISTRER).</summary>
public interface IInputRecorder
{
    bool IsRecording { get; }

    /// <summary>
    /// Démarre le décompte (si configuré) puis l'enregistrement. La tâche se termine une fois l'enregistrement
    /// réellement actif ; annulable pendant le décompte (avant que quoi que ce soit ne soit capturé).
    /// </summary>
    Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default);

    /// <summary>Arrête l'enregistrement (ou annule un décompte en cours) et renvoie les commandes capturées.</summary>
    IReadOnlyList<MacroCommand> Stop();

    /// <summary>Une commande vient d'être capturée (pour un affichage en direct dans la grille).</summary>
    event EventHandler<MacroCommand>? CommandRecorded;

    /// <summary>Secondes restantes pendant le décompte de démarrage (0 = enregistrement effectivement démarré).</summary>
    event EventHandler<int>? CountdownTick;
}
