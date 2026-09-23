using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Implémentation réelle de <see cref="IScriptRunner"/> : compile et exécute le code avec Roslyn scripting
/// (Microsoft.CodeAnalysis.CSharp.Scripting), globals <see cref="ScriptGlobals"/>.
///
/// Annulation (arrêt d'urgence ET délai maximal, réunis dans le même jeton exposé par
/// <see cref="ScriptGlobals.CancellationToken"/>, voir ci-dessous) : **garantie seulement pour un script qui
/// coopère**, en appelant lui-même <c>CancellationToken.ThrowIfCancellationRequested()</c> (ou en attendant une
/// opération qui observe ce jeton, ex. <c>Task.Delay(ms, CancellationToken)</c>). Vérifié empiriquement dans cet
/// environnement (voir les tests de <c>RoslynScriptRunnerTests</c>) : le comportement de Roslyn scripting quand le
/// jeton passé à <c>CSharpScript.RunAsync</c> n'est PAS observé par le script lui-même s'est révélé incohérent
/// selon la forme exacte du code (parfois interrompu entre deux instructions de haut niveau, parfois seulement
/// après la fin naturelle d'une instruction synchrone longue, jamais documenté officiellement) — on ne peut donc
/// pas promettre qu'un script purement synchrone et non coopératif (ex. une boucle infinie sans vérifier le jeton)
/// sera interrompu avant sa fin naturelle. Limite connue de l'annulation coopérative .NET (et de Roslyn scripting
/// en particulier), documentée plutôt que cachée (voir PROMPT.md étape 5).
/// </summary>
public sealed class RoslynScriptRunner : IScriptRunner
{
    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Task).Assembly,
            typeof(ScriptGlobals).Assembly)
        .WithImports("System", "System.Linq", "System.Threading", "System.Threading.Tasks", "V0XMacroRecorder.Core.Playback", "V0XMacroRecorder.Core.Macros");

    public async Task<ScriptRunResult> RunAsync(string code, ScriptGlobals globals, int timeoutMs, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(Math.Max(1, timeoutMs));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        // Le script voit le jeton combiné (arrêt d'urgence + délai maximal), pas seulement celui reçu à la
        // construction de ScriptGlobals : un script coopératif peut ainsi se rendre réactif aux deux à la fois.
        globals.CancellationToken = linked.Token;
        try
        {
            // Task.Run : le script s'exécute hors du contexte de synchronisation appelant (jamais de risque de
            // blocage si un appelant attend le résultat de façon synchrone, ex. le bouton « Tester » de l'éditeur).
            await Task.Run(() => CSharpScript.RunAsync(code, Options, globals, cancellationToken: linked.Token), linked.Token).ConfigureAwait(false);
            return new ScriptRunResult(true, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ScriptRunResult(false, "Délai d'exécution du script dépassé.");
        }
        catch (CompilationErrorException ex)
        {
            return new ScriptRunResult(false, string.Join(" ", ex.Diagnostics));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ScriptRunResult(false, ex.Message);
        }
    }
}
