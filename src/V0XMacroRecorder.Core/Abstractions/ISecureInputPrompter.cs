namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Invite masquée (mot de passe) affichée à la lecture pour une commande « Saisie protégée » en mode « demander à la lecture ».</summary>
public interface ISecureInputPrompter
{
    /// <summary>Renvoie le secret saisi, ou null si l'utilisateur annule.</summary>
    string? PromptForSecret(string title, string message);
}
