namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Élévation UAC à la demande (une app non-admin ne peut pas envoyer d'entrées à une fenêtre administrateur).</summary>
public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>Relance l'application en administrateur (UAC) ; l'appelant doit alors fermer l'instance courante. Faux si l'utilisateur a refusé l'invite UAC ou en cas d'échec.</summary>
    bool RelaunchElevated();
}
