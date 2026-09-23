namespace V0XMacroRecorder.Core.Macros;

/// <summary>Noms français des codes de touches virtuelles Windows (VK_*) pour l'affichage.</summary>
public static class VirtualKeyNames
{
    private static readonly Dictionary<int, string> Names = Build();

    public static string GetName(int virtualKey) =>
        Names.TryGetValue(virtualKey, out var name) ? name : $"Touche 0x{virtualKey:X2}";

    /// <summary>Libellé d'un raccourci, par exemple « Ctrl+Maj+A ».</summary>
    public static string Format(KeyModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Maj");
        }

        if (modifiers.HasFlag(KeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(GetName(virtualKey));
        return string.Join("+", parts);
    }

    private static Dictionary<int, string> Build()
    {
        var names = new Dictionary<int, string>
        {
            [0x08] = "Retour arrière",
            [0x09] = "Tab",
            [0x0D] = "Entrée",
            [0x10] = "Maj",
            [0x11] = "Ctrl",
            [0x12] = "Alt",
            [0x13] = "Pause",
            [0x14] = "Verr. maj",
            [0x1B] = "Échap",
            [0x20] = "Espace",
            [0x21] = "Page précédente",
            [0x22] = "Page suivante",
            [0x23] = "Fin",
            [0x24] = "Début",
            [0x25] = "Flèche gauche",
            [0x26] = "Flèche haut",
            [0x27] = "Flèche droite",
            [0x28] = "Flèche bas",
            [0x2C] = "Impr. écran",
            [0x2D] = "Inser",
            [0x2E] = "Suppr",
            [0x5B] = "Win gauche",
            [0x5C] = "Win droite",
            [0x5D] = "Menu contextuel",
            [0x6A] = "Pavé num. *",
            [0x6B] = "Pavé num. +",
            [0x6D] = "Pavé num. -",
            [0x6E] = "Pavé num. .",
            [0x6F] = "Pavé num. /",
            [0x90] = "Verr. num",
            [0x91] = "Arrêt défil.",
            [0xA0] = "Maj gauche",
            [0xA1] = "Maj droite",
            [0xA2] = "Ctrl gauche",
            [0xA3] = "Ctrl droite",
            [0xA4] = "Alt gauche",
            [0xA5] = "Alt droite",
        };

        for (var i = 0; i <= 9; i++)
        {
            names[0x30 + i] = i.ToString();
            names[0x60 + i] = $"Pavé num. {i}";
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            names[c] = c.ToString();
        }

        for (var i = 1; i <= 24; i++)
        {
            names[0x6F + i] = $"F{i}";
        }

        return names;
    }
}
