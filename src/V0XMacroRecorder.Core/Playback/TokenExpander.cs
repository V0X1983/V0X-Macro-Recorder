using System.Text.RegularExpressions;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Expansion des jetons <c>{date}</c>, <c>{clipboard}</c> et <c>{var:nom}</c> dans un texte (commande Texte,
/// arguments de programme, presse-papiers…). Un jeton inconnu ou malformé est laissé tel quel : ne lève jamais.
/// </summary>
public static partial class TokenExpander
{
    [GeneratedRegex(@"\{(date|clipboard|var:[^}]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    public static string Expand(string text, VariableStore variables, IClipboardService? clipboard, Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var now = clock ?? (() => DateTime.Now);
        return TokenPattern().Replace(text, match =>
        {
            var token = match.Groups[1].Value;
            if (token.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                return now().ToString("yyyy-MM-dd HH:mm:ss");
            }

            if (token.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
            {
                return clipboard?.GetText() ?? "";
            }

            var name = token[(token.IndexOf(':') + 1)..];
            return variables.Get(name);
        });
    }
}
