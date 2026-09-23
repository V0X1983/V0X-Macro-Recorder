namespace V0XMacroRecorder.Core.CommandLine;

/// <summary>
/// Requête <c>--play "chemin.v0xmacro" [--silent] [--repeat N]</c> (étape 6) : lance une macro en ligne de
/// commande, typiquement depuis une tâche planifiée. <see cref="Silent"/> signifie « sans fenêtre visible,
/// quitter à la fin » ; sans lui, l'application s'ouvre normalement et démarre la lecture.
/// </summary>
public sealed record PlayRequest(string MacroFilePath, bool Silent, int? RepeatOverride);

/// <summary>Analyse pure des arguments de ligne de commande (aucune dépendance Win32 : testable directement).</summary>
public static class CommandLineArgs
{
    /// <summary>Renvoie la requête <c>--play</c> si présente dans <paramref name="args"/>, sinon null.</summary>
    public static PlayRequest? ParsePlay(IReadOnlyList<string> args)
    {
        var playIndex = IndexOfOption(args, "--play");
        if (playIndex < 0 || playIndex + 1 >= args.Count)
        {
            return null;
        }

        var path = args[playIndex + 1];
        var silent = args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase));

        int? repeat = null;
        var repeatIndex = IndexOfOption(args, "--repeat");
        if (repeatIndex >= 0 && repeatIndex + 1 < args.Count
            && int.TryParse(args[repeatIndex + 1], out var parsedRepeat) && parsedRepeat > 0)
        {
            repeat = parsedRepeat;
        }

        return new PlayRequest(path, silent, repeat);
    }

    private static int IndexOfOption(IReadOnlyList<string> args, string option)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
