using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Tests.Fakes;

/// <summary>
/// Remplace Roslyn scripting dans les tests de <see cref="MacroPlayer"/> : n'exécute jamais réellement de C#,
/// se contente d'enregistrer l'appel et d'exécuter <see cref="OnRun"/> (s'il est fourni) contre les mêmes
/// <see cref="ScriptGlobals"/> qu'un vrai script recevrait, pour vérifier le câblage sans dépendre du compilateur.
/// </summary>
public sealed class FakeScriptRunner : IScriptRunner
{
    public List<(string Code, int TimeoutMs)> Calls { get; } = [];

    public ScriptRunResult Result { get; set; } = new(true, null);

    public Action<ScriptGlobals>? OnRun { get; set; }

    public Task<ScriptRunResult> RunAsync(string code, ScriptGlobals globals, int timeoutMs, CancellationToken cancellationToken)
    {
        Calls.Add((code, timeoutMs));
        OnRun?.Invoke(globals);
        return Task.FromResult(Result);
    }
}
