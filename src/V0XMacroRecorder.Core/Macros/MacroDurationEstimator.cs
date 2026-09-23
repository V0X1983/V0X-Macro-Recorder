namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Estimation de la durée d'une macro (barre d'état, étape 7) : purement indicative, à vitesse 1× et une seule
/// répétition. N'entre jamais dans le compte les attentes dont la durée réelle dépend de l'environnement
/// (fenêtre/pixel/image/touche, Pause utilisateur, Programme en mode « attendre la fin ») : seuls les délais fixes
/// (avant chaque commande, Attente à durée fixe, saisie de texte caractère par caractère) sont additionnés.
/// </summary>
public static class MacroDurationEstimator
{
    public static long EstimateMilliseconds(IReadOnlyList<MacroCommand> commands)
    {
        long total = 0;
        foreach (var command in commands)
        {
            total += command.DelayMs;
            total += command switch
            {
                WaitCommand { Mode: WaitMode.FixedDelay } wait => wait.DurationMs + wait.RandomExtraMs / 2,
                TextCommand text => (long)text.Text.Length * text.CharacterDelayMs,
                _ => 0,
            };
        }

        return total;
    }
}
