namespace V0XMacroRecorder.Core.Models;

/// <param name="InstallerUrl">Adresse de téléchargement de l'installeur (.exe) joint à la release, si présent.</param>
/// <param name="InstallerSha256">Empreinte SHA-256 annoncée par GitHub pour l'installeur, si disponible.</param>
public sealed record LatestReleaseInfo(
    bool Success,
    string? Version,
    string? ReleaseUrl,
    string Message,
    string? InstallerUrl = null,
    string? InstallerSha256 = null);
