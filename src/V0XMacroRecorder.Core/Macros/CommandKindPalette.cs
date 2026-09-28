namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Couleur associée à chaque type de commande (barre verticale à gauche de chaque ligne dans la grille),
/// personnalisable par l'utilisateur (<c>AppSettings.CommandKindColors</c>, surcharges uniquement — vide par
/// défaut). Clé = <see cref="MacroCommand.Kind"/> (discriminant JSON, jamais renommé).
/// </summary>
public static class CommandKindPalette
{
    /// <summary>Couleur par défaut (catégorielle, choisie pour rester distinguable en thème clair et sombre).</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>
    {
        ["mouse"] = "#FF3B82F6",
        ["keyboard"] = "#FFF59E0B",
        ["wait"] = "#FF64748B",
        ["program"] = "#FF6366F1",
        ["window"] = "#FF0EA5E9",
        ["clipboard"] = "#FF14B8A6",
        ["text"] = "#FF8B5CF6",
        ["secureinput"] = "#FFDC2626",
        ["pixel"] = "#FFEC4899",
        ["image"] = "#FFD946EF",
        ["url"] = "#FF06B6D4",
        ["sound"] = "#FF84CC16",
        ["message"] = "#FFEAB308",
        ["script"] = "#FF7C3AED",
        ["if"] = "#FF22C55E",
        ["loop"] = "#FFF97316",
        ["variable"] = "#FFA855F7",
        ["label"] = "#FF92400E",
        ["goto"] = "#FFEF4444",
        ["call"] = "#FF475569",
        ["stop"] = "#FF991B1B",
        ["pause"] = "#FF9CA3AF",
        ["comment"] = "#FF9CA3AF",
    };

    private const string FallbackColor = "#FF9AA4B2";

    /// <summary>Rabat un type sans entrée dédiée sur la couleur de son type parent (ex. Sinon/Fin si -> Si).</summary>
    private static string NormalizeKind(string kind) => kind switch
    {
        "else" or "endif" => "if",
        "endloop" => "loop",
        _ => kind,
    };

    /// <summary>Couleur effective pour <paramref name="kind"/> : surcharge utilisateur, sinon valeur par défaut.</summary>
    public static string Resolve(string kind, IReadOnlyDictionary<string, string>? overrides = null)
    {
        var normalized = NormalizeKind(kind);

        if (overrides is not null && overrides.TryGetValue(normalized, out var custom) && !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        return Defaults.TryGetValue(normalized, out var value) ? value : FallbackColor;
    }
}
