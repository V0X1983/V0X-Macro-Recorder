using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Lecture/écriture du format JSON versionné des macros. Pour changer le format : incrémenter
/// <see cref="CurrentSchemaVersion"/> et ajouter dans <c>Migrations</c> la fonction qui transforme
/// la version N en N+1 (les fichiers plus anciens sont migrés à l'ouverture, jamais rejetés).
/// </summary>
public static class MacroSerializer
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Migrations : la clé N transforme un document de version N en version N+1 (aucune pour l'instant).</summary>
    private static readonly IReadOnlyDictionary<int, Action<JsonObject>> Migrations =
        new Dictionary<int, Action<JsonObject>>();

    public static string Serialize(Macro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        macro.SchemaVersion = CurrentSchemaVersion;
        return JsonSerializer.Serialize(macro, Options);
    }

    public static Macro Deserialize(string json) => Deserialize(json, CurrentSchemaVersion, Migrations);

    internal static Macro Deserialize(string json, int currentVersion, IReadOnlyDictionary<int, Action<JsonObject>> migrations)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                ?? throw new MacroFormatException("Ce fichier n'est pas une macro V0X valide.");
        }
        catch (JsonException ex)
        {
            throw new MacroFormatException("Ce fichier est corrompu ou n'est pas une macro V0X.", ex);
        }

        var version = ReadVersion(root);
        if (version > currentVersion)
        {
            throw new MacroFormatException(
                $"Cette macro a été créée par une version plus récente de V0X Macro Recorder (format {version}). Mettez le logiciel à jour pour l'ouvrir.");
        }

        for (; version < currentVersion; version++)
        {
            if (!migrations.TryGetValue(version, out var migrate))
            {
                throw new MacroFormatException($"Le format {version} de cette macro n'est plus pris en charge.");
            }

            migrate(root);
            root["schemaVersion"] = version + 1;
        }

        try
        {
            return root.Deserialize<Macro>(Options)
                ?? throw new MacroFormatException("Ce fichier n'est pas une macro V0X valide.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new MacroFormatException("Cette macro contient des commandes illisibles ou inconnues.", ex);
        }
    }

    /// <summary>Sérialise une sélection de commandes (presse-papiers).</summary>
    public static string SerializeCommands(IEnumerable<MacroCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return JsonSerializer.Serialize(commands.ToList(), Options);
    }

    public static IReadOnlyList<MacroCommand> DeserializeCommands(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<MacroCommand>>(json, Options)
                ?? throw new MacroFormatException("Aucune commande à coller.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new MacroFormatException("Le contenu du presse-papiers n'est pas une liste de commandes V0X.", ex);
        }
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var version) && version >= 1)
        {
            return version;
        }

        throw new MacroFormatException("Ce fichier n'est pas une macro V0X valide (version de format absente).");
    }
}
