namespace V0XMacroRecorder.Services.Native;

/// <summary>Emplacements disque standard de l'application, centralisés pour éviter les chemins en dur.</summary>
public static class AppPaths
{
    /// <summary>Dossier de données ; peut être redéfini avant tout accès (tests).</summary>
    public static string RootFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "V0XMacroRecorder");

    public static string LogsFolder => EnsureExists(Path.Combine(RootFolder, "logs"));

    public static string ConfigFilePath => Path.Combine(EnsureExists(RootFolder), "config.json");

    private static string EnsureExists(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
