namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Démarrage avec Windows (clé HKCU Run, jamais besoin de droits administrateur).</summary>
public interface IStartupRegistrationService
{
    /// <summary>Active/désactive le lancement au démarrage ; faux si l'écriture registre a échoué.</summary>
    bool Apply(bool enabled, bool startMinimized);

    bool IsRegistered();
}
