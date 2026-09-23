using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Interroge la dernière publication GitHub Releases d'un dépôt configuré par l'utilisateur.</summary>
public interface IUpdateChecker
{
    Task<LatestReleaseInfo> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge l'installeur d'une release dans un dossier temporaire et retourne son chemin.
    /// Refuse toute adresse hors GitHub et vérifie l'empreinte SHA-256 quand elle est connue.
    /// </summary>
    Task<string> DownloadInstallerAsync(LatestReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
