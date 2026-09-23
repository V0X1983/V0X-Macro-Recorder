using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Résultat de l'exécution d'un script C# (commande Script, étape 5, ou bouton « Tester » de son éditeur).</summary>
public sealed record ScriptRunResult(bool Success, string? ErrorMessage);

/// <summary>
/// Exécute le code d'une <see cref="Macros.ScriptCommand"/> (Roslyn scripting, implémentation dans Services)
/// avec l'objet <see cref="ScriptGlobals"/> comme contexte "globals" exposé au script.
/// </summary>
public interface IScriptRunner
{
    /// <summary>
    /// Compile et exécute <paramref name="code"/>. Renvoie un échec (jamais d'exception) en cas d'erreur de
    /// compilation, d'exception levée par le script ou de dépassement de <paramref name="timeoutMs"/>.
    /// L'annulation de <paramref name="cancellationToken"/> (arrêt d'urgence) se propage normalement.
    /// </summary>
    Task<ScriptRunResult> RunAsync(string code, ScriptGlobals globals, int timeoutMs, CancellationToken cancellationToken);
}
