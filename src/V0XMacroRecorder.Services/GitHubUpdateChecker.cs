using System.Net.Http;
using System.Text.Json;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Vérifie la dernière publication GitHub Releases du dépôt configuré dans les Paramètres (par défaut
/// le dépôt officiel, voir <see cref="AppSettings.DefaultUpdateOwner"/>). La vérification n'a lieu
/// que lorsque l'utilisateur la demande.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly HttpClient DownloadHttp = new() { Timeout = TimeSpan.FromMinutes(15) };

    public async Task<LatestReleaseInfo> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            owner = AppSettings.DefaultUpdateOwner;
        }

        if (string.IsNullOrWhiteSpace(repo))
        {
            repo = AppSettings.DefaultUpdateRepo;
        }

        owner = owner.Trim();
        repo = repo.Trim();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd("V0XMacroRecorder-UpdateChecker");

            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new LatestReleaseInfo(false, null, null, "Aucune version publiée pour le moment sur le dépôt de mise à jour.");
                }

                return new LatestReleaseInfo(false, null, null, $"Impossible de vérifier les mises à jour (code {(int)response.StatusCode}).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var tagName = doc.RootElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            var htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                return new LatestReleaseInfo(false, null, null, "Réponse inattendue du serveur de mises à jour.");
            }

            string? installerUrl = null;
            string? installerSha256 = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (name is null
                        || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        || !name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    installerUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() : null;
                    var digest = asset.TryGetProperty("digest", out var digestProp) ? digestProp.GetString() : null;
                    installerSha256 = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                        ? digest["sha256:".Length..]
                        : null;
                    break;
                }
            }

            var version = tagName.TrimStart('v', 'V');
            return new LatestReleaseInfo(true, version, htmlUrl, $"Dernière version publiée : {version}.", installerUrl, installerSha256);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new LatestReleaseInfo(false, null, null, $"Échec de la vérification : {ex.Message}");
        }
    }

    public async Task<string> DownloadInstallerAsync(LatestReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(release.InstallerUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Aucun installeur téléchargeable depuis GitHub n'est joint à cette version.");
        }

        var folder = Path.Combine(Path.GetTempPath(), "V0XMacroRecorder-update");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(uri.LocalPath));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("V0XMacroRecorder-UpdateChecker");
        using var response = await DownloadHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                read += n;
                if (total is > 0)
                {
                    progress?.Report((double)read / total.Value);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(release.InstallerSha256))
        {
            string hash;
            await using (var check = File.OpenRead(path))
            {
                hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(check, cancellationToken));
            }

            // Le flux doit être refermé avant de supprimer le fichier (verrou Windows), d'où la portée explicite ci-dessus.
            if (!hash.Equals(release.InstallerSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new InvalidOperationException("Le fichier téléchargé ne correspond pas à l'empreinte publiée : mise à jour annulée.");
            }
        }

        return path;
    }
}
