using System.Diagnostics;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Services;
using V0XMacroRecorder.Tests.Fakes;

namespace V0XMacroRecorder.Tests;

/// <summary>
/// Contrairement à l'injection réelle (SendInput, voir PROMPT.md), la compilation/exécution Roslyn ne dépend
/// d'aucune fenêtre ni pilote Windows : ces tests exercent le vrai <see cref="RoslynScriptRunner"/> (pas un faux),
/// avec des services Win32 factices pour vérifier le câblage de <see cref="ScriptGlobals"/>.
/// </summary>
public sealed class RoslynScriptRunnerTests
{
    private static ScriptGlobals NewGlobals(FakeInputSimulator? sim = null, VariableStore? variables = null, Action<string>? log = null, CancellationToken ct = default) =>
        new(sim ?? new FakeInputSimulator(), new FakeWindowFinder(), new FakeWindowController(), new FakeClipboardService(), variables ?? new VariableStore(), log ?? (_ => { }), ct);

    [Fact]
    public async Task Valid_script_runs_and_manipulates_the_real_api_surface()
    {
        var sim = new FakeInputSimulator();
        var variables = new VariableStore();
        var runner = new RoslynScriptRunner();

        var result = await runner.RunAsync(
            "Mouse.MoveTo(10, 20); Keyboard.Type(\"hi\"); Vars.Set(\"x\", \"1\"); Log.Info(\"ok\");",
            NewGlobals(sim, variables),
            timeoutMs: 5000,
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal((10, 20), sim.GetCursorPosition());
        Assert.Equal("hi", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
        Assert.Equal("1", variables.Get("x"));
    }

    [Fact]
    public async Task Compilation_error_is_reported_as_a_failure_not_an_exception()
    {
        var runner = new RoslynScriptRunner();

        var result = await runner.RunAsync("this is not C#;;;", NewGlobals(), timeoutMs: 5000, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task Thrown_exception_is_reported_as_a_failure()
    {
        var runner = new RoslynScriptRunner();

        var result = await runner.RunAsync("throw new InvalidOperationException(\"boom\");", NewGlobals(), timeoutMs: 5000, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("boom", result.ErrorMessage);
    }

    /// <summary>
    /// Le seul comportement d'annulation qu'on peut garantir (voir commentaire de <see cref="RoslynScriptRunner"/> :
    /// le comportement natif de Roslyn scripting sur du code non coopératif s'est révélé incohérent à l'usage) :
    /// un script qui vérifie lui-même <c>CancellationToken</c> est interrompu quand le délai maximal s'écoule —
    /// <see cref="RoslynScriptRunner"/> lui expose le jeton combiné (arrêt d'urgence + délai) via ce même membre.
    /// </summary>
    [Fact]
    public async Task Cooperative_script_is_interrupted_when_the_timeout_elapses()
    {
        var runner = new RoslynScriptRunner();
        var stopwatch = Stopwatch.StartNew();

        var result = await runner.RunAsync(
            "while (true) { CancellationToken.ThrowIfCancellationRequested(); System.Threading.Thread.Sleep(10); }",
            NewGlobals(),
            timeoutMs: 100,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("délai", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"La boucle infinie aurait dû être interrompue bien avant (écoulé : {stopwatch.ElapsedMilliseconds} ms).");
    }

    /// <summary>Même mécanisme qu'au-dessus, mais déclenché par l'arrêt d'urgence pendant l'exécution (pas le délai maximal).</summary>
    [Fact]
    public async Task Cooperative_script_observes_a_mid_run_emergency_stop()
    {
        var runner = new RoslynScriptRunner();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                "while (true) { CancellationToken.ThrowIfCancellationRequested(); System.Threading.Thread.Sleep(10); }",
                NewGlobals(ct: cts.Token),
                timeoutMs: 30000,
                cts.Token));
    }

    [Fact]
    public async Task Emergency_stop_cancellation_propagates_instead_of_being_swallowed()
    {
        var runner = new RoslynScriptRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync("System.Threading.Thread.Sleep(10);", NewGlobals(ct: cts.Token), timeoutMs: 5000, cts.Token));
    }
}
