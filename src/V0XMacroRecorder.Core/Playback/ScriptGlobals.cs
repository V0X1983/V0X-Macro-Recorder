using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Objet "globals" exposé à une commande Script C# (étape 5, <see cref="IScriptRunner"/>) : API volontairement
/// restreinte à la souris, au clavier, aux fenêtres, au presse-papiers, aux variables de macro et à un journal
/// simple — jamais d'accès direct aux interfaces Win32 (pas de P/Invoke, pas de fichiers hors Vars/Log). Les noms
/// des membres publics (Mouse/Keyboard/Window/Clipboard/Vars/Log/CancellationToken) sont l'API documentée pour
/// l'utilisateur : ne jamais les renommer.
/// </summary>
public sealed class ScriptGlobals
{
    public ScriptGlobals(
        IInputSimulator simulator,
        IWindowFinder windowFinder,
        IWindowController windowController,
        IClipboardService clipboard,
        VariableStore variables,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        Mouse = new ScriptMouseApi(simulator);
        Keyboard = new ScriptKeyboardApi(simulator);
        Window = new ScriptWindowApi(windowFinder, windowController);
        Clipboard = new ScriptClipboardApi(clipboard);
        Vars = new ScriptVariablesApi(variables);
        Log = new ScriptLogApi(log);
        CancellationToken = cancellationToken;
    }

    public ScriptMouseApi Mouse { get; }

    public ScriptKeyboardApi Keyboard { get; }

    public ScriptWindowApi Window { get; }

    public ScriptClipboardApi Clipboard { get; }

    public ScriptVariablesApi Vars { get; }

    public ScriptLogApi Log { get; }

    /// <summary>
    /// Combine l'arrêt d'urgence ET le délai maximal de la commande (<see cref="IScriptRunner"/> le remplace par le
    /// jeton lié juste avant d'exécuter le script) : un script coopératif qui appelle
    /// <c>CancellationToken.ThrowIfCancellationRequested()</c> dans une boucle longue reste réactif aux deux. Un
    /// script purement synchrone qui n'observe jamais ce jeton (ni n'attend quoi que ce soit) n'est interrompu ni
    /// par l'arrêt d'urgence ni par le délai maximal — limite connue de l'annulation coopérative .NET, documentée
    /// plutôt que cachée (voir PROMPT.md étape 5).
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}

/// <summary>Souris exposée au script (mêmes primitives que <see cref="MouseCommand"/>, sans repère de fenêtre).</summary>
public sealed class ScriptMouseApi
{
    private const int ClickHoldMs = 30;
    private readonly IInputSimulator _simulator;

    internal ScriptMouseApi(IInputSimulator simulator) => _simulator = simulator;

    public (int X, int Y) Position => _simulator.GetCursorPosition();

    public void MoveTo(int x, int y) => _simulator.MoveMouseTo(x, y);

    public void Click(MouseButton button = MouseButton.Left)
    {
        _simulator.MouseButton(button, true);
        Thread.Sleep(ClickHoldMs);
        _simulator.MouseButton(button, false);
    }

    public void Down(MouseButton button = MouseButton.Left) => _simulator.MouseButton(button, true);

    public void Up(MouseButton button = MouseButton.Left) => _simulator.MouseButton(button, false);

    /// <summary>Crans de molette : positif vers le haut, négatif vers le bas.</summary>
    public void Wheel(int notches) => _simulator.MouseWheel(notches);
}

/// <summary>Clavier exposé au script : par code de touche virtuelle (VK_*), sans code de balayage (déduit par l'implémentation réelle).</summary>
public sealed class ScriptKeyboardApi
{
    private readonly IInputSimulator _simulator;

    internal ScriptKeyboardApi(IInputSimulator simulator) => _simulator = simulator;

    public void Press(int virtualKey)
    {
        _simulator.KeyEvent(virtualKey, 0, false, true);
        _simulator.KeyEvent(virtualKey, 0, false, false);
    }

    public void Down(int virtualKey) => _simulator.KeyEvent(virtualKey, 0, false, true);

    public void Up(int virtualKey) => _simulator.KeyEvent(virtualKey, 0, false, false);

    /// <summary>Saisie Unicode directe, indépendante du clavier physique (comme la commande Texte).</summary>
    public void Type(string text)
    {
        foreach (var character in text)
        {
            _simulator.TypeCharacter(character);
        }
    }
}

/// <summary>Fenêtres exposées au script, retrouvées par titre (partiel) et/ou classe Win32, comme la commande Fenêtre.</summary>
public sealed class ScriptWindowApi
{
    private readonly IWindowFinder _finder;
    private readonly IWindowController _controller;

    internal ScriptWindowApi(IWindowFinder finder, IWindowController controller)
    {
        _finder = finder;
        _controller = controller;
    }

    public bool Exists(string? title, string? className = null) => _finder.FindWindow(title, className) is not null;

    public void Activate(string? title, string? className = null)
    {
        if (_finder.FindWindow(title, className) is { } handle)
        {
            _finder.Activate(handle);
        }
    }

    public void Close(string? title, string? className = null) => WithWindow(title, className, _controller.Close);

    public void Minimize(string? title, string? className = null) => WithWindow(title, className, _controller.Minimize);

    public void Maximize(string? title, string? className = null) => WithWindow(title, className, _controller.Maximize);

    public void Restore(string? title, string? className = null) => WithWindow(title, className, _controller.Restore);

    public void MoveResize(string? title, int x, int y, int width, int height, string? className = null)
    {
        if (_finder.FindWindow(title, className) is { } handle)
        {
            _controller.MoveResize(handle, x, y, width, height);
        }
    }

    private void WithWindow(string? title, string? className, Func<nint, bool> action)
    {
        if (_finder.FindWindow(title, className) is { } handle)
        {
            action(handle);
        }
    }
}

/// <summary>Presse-papiers exposé au script (mêmes primitives que la commande Presse-papiers).</summary>
public sealed class ScriptClipboardApi
{
    private readonly IClipboardService _clipboard;

    internal ScriptClipboardApi(IClipboardService clipboard) => _clipboard = clipboard;

    public string GetText() => _clipboard.GetText();

    public void SetText(string text) => _clipboard.SetText(text);
}

/// <summary>Variables de macro exposées au script : partagent le même <see cref="VariableStore"/> que le reste de la lecture.</summary>
public sealed class ScriptVariablesApi
{
    private readonly VariableStore _variables;

    internal ScriptVariablesApi(VariableStore variables) => _variables = variables;

    public string Get(string name) => _variables.Get(name);

    public void Set(string name, string value) => _variables.Set(name, value);

    public double GetNumber(string name) => _variables.TryGetNumber(name, out var value) ? value : 0;
}

/// <summary>Journal simple exposé au script : relayé comme avertissement de lecture (bandeau de lecture), pas de niveau distinct pour l'instant.</summary>
public sealed class ScriptLogApi
{
    private readonly Action<string> _log;

    internal ScriptLogApi(Action<string> log) => _log = log;

    public void Info(string message) => _log($"Script : {message}");

    public void Warning(string message) => _log($"Script (avertissement) : {message}");
}
