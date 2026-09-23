namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Lance un programme, une commande shell, ou ouvre une URL/un fichier (commandes Programme/fichier et Ouvrir une URL).</summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Lance <paramref name="path"/>. <paramref name="shellExecute"/> vrai = ouverture via le shell (URL, fichier,
    /// commande) ; faux = exécutable direct avec <paramref name="arguments"/>/<paramref name="workingDirectory"/>.
    /// Si <paramref name="waitForExit"/>, attend au plus <paramref name="timeoutMs"/> ms et renvoie le code de
    /// sortie (null si le délai est dépassé ou si l'attente n'est pas demandée).
    /// </summary>
    Task<int?> LaunchAsync(string path, string? arguments, string? workingDirectory, bool shellExecute, bool waitForExit, int timeoutMs, CancellationToken ct);
}
