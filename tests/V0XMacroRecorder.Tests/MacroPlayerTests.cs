using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Tests.Fakes;

namespace V0XMacroRecorder.Tests;

public sealed class MacroPlayerTests
{
    /// <summary>Renvoie toujours le minimum de la plage : rend <see cref="WaitCommand.RandomExtraMs"/> déterministe.</summary>
    private sealed class MinRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }

    private static (MacroPlayer Player, FakeInputSimulator Sim, FakeWindowFinder Windows, FakeElevationService Elevation, List<int> Delays, FakeKeyWaiter KeyWaiter) NewPlayer(
        FakeElevationService? elevation = null, Random? random = null, FakeKeyWaiter? keyWaiter = null,
        FakeClipboardService? clipboard = null, Func<DateTime>? clock = null, FakeProcessLauncher? launcher = null,
        FakeWindowController? windowController = null, FakePixelReader? pixelReader = null,
        FakeSoundPlayer? soundPlayer = null, FakeMessageBoxService? messageBox = null, FakeImageSearcher? imageSearcher = null,
        FakeFileLineSource? fileLineSource = null, FakeMacroLoader? macroLoader = null, FakeScriptRunner? scriptRunner = null,
        FakeSessionLockService? sessionLock = null, FakeSecureInputPrompter? securePrompter = null, FakeDataProtector? dataProtector = null)
    {
        var sim = new FakeInputSimulator();
        var windows = new FakeWindowFinder();
        var elevationService = elevation ?? new FakeElevationService();
        var keys = keyWaiter ?? new FakeKeyWaiter();
        var clip = clipboard ?? new FakeClipboardService();
        var proc = launcher ?? new FakeProcessLauncher();
        var winCtrl = windowController ?? new FakeWindowController();
        var pixels = pixelReader ?? new FakePixelReader();
        var sound = soundPlayer ?? new FakeSoundPlayer();
        var msgBox = messageBox ?? new FakeMessageBoxService();
        var images = imageSearcher ?? new FakeImageSearcher();
        var files = fileLineSource ?? new FakeFileLineSource();
        var macros = macroLoader ?? new FakeMacroLoader();
        var delays = new List<int>();
        Task Delay(int ms, CancellationToken ct)
        {
            delays.Add(ms);
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        var player = new MacroPlayer(sim, windows, elevationService, keys, clip, proc, winCtrl, pixels, sound, msgBox, images, files, macros, Delay, random ?? new MinRandom(), clock, scriptRunner, sessionLock, securePrompter, dataProtector);
        return (player, sim, windows, elevationService, delays, keys);
    }

    private static MouseCommand Move(int x, int y, int delayMs = 0) =>
        new() { Action = MouseAction.Move, X = x, Y = y, DelayMs = delayMs };

    // ------------------------------------------------------------------------------------------------ Ordre & délais

    [Fact]
    public async Task Executes_commands_in_order_and_waits_the_configured_delay_first()
    {
        var (player, sim, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { Move(1, 1, 100), Move(2, 2, 200) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([100, 200], delays);
        Assert.Equal([new MoveEvent(1, 1), new MoveEvent(2, 2)], sim.Events);
    }

    [Fact]
    public async Task CommandStarted_fires_with_the_index_of_each_command()
    {
        var (player, _, _, _, _, _) = NewPlayer();
        var started = new List<int>();
        player.CommandStarted += (_, i) => started.Add(i);
        var commands = new List<MacroCommand> { Move(0, 0), Move(1, 1), Move(2, 2) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([0, 1, 2], started);
    }

    [Fact]
    public async Task RepeatCount_replays_every_command_and_waits_between_repeats_but_not_after_the_last()
    {
        var (player, sim, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { Move(1, 1) };
        var repeats = new List<int>();
        player.RepeatStarted += (_, n) => repeats.Add(n);

        await player.RunAsync(commands, new PlaybackOptions { RepeatCount = 3, DelayBetweenRepeatsMs = 50 });

        Assert.Equal([1, 2, 3], repeats);
        Assert.Equal(3, sim.Events.Count);
        Assert.Equal([50, 50], delays); // entre 1→2 et 2→3, jamais après la 3e.
    }

    [Fact]
    public async Task InfiniteLoop_keeps_running_until_the_token_is_cancelled()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        using var cts = new CancellationTokenSource();
        var repeats = 0;
        player.RepeatStarted += (_, _) =>
        {
            repeats++;
            if (repeats >= 5)
            {
                cts.Cancel();
            }
        };
        var commands = new List<MacroCommand> { Move(1, 1) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            player.RunAsync(commands, new PlaybackOptions { InfiniteLoop = true }, cancellationToken: cts.Token));

        Assert.Equal(5, repeats); // la 5e répétition a démarré (RepeatStarted a fait annuler)…
        Assert.Equal(4, sim.Events.Count); // … mais l'annulation coupe avant que sa commande ne s'exécute.
    }

    [Fact]
    public async Task NoDelay_zeroes_every_kind_of_delay()
    {
        var (player, _, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            Move(1, 1, 500),
            new WaitCommand { DurationMs = 1000, RandomExtraMs = 500 },
            new TextCommand { Text = "ab", CharacterDelayMs = 200 },
        };

        await player.RunAsync(commands, new PlaybackOptions { NoDelay = true });

        Assert.Empty(delays); // tous ramenés à 0 ms, donc jamais même soumis à l'horloge (voir DelayAsync).
    }

    [Fact]
    public async Task SpeedMultiplier_scales_every_delay()
    {
        var (player, _, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { Move(1, 1, 100) };

        await player.RunAsync(commands, new PlaybackOptions { SpeedMultiplier = 2.0 });

        Assert.Equal([50], delays);
    }

    // ------------------------------------------------------------------------------------------------ Souris

    [Fact]
    public async Task Click_presses_waits_and_releases()
    {
        var (player, sim, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { new MouseCommand { Action = MouseAction.Click, Button = MouseButton.Right, X = 10, Y = 20 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(
        [
            new MoveEvent(10, 20),
            new ButtonEvent(MouseButton.Right, true),
            new ButtonEvent(MouseButton.Right, false),
        ], sim.Events);
        Assert.Equal([30], delays); // maintien du clic (ClickHoldMs).
    }

    [Fact]
    public async Task DoubleClick_produces_two_clicks_separated_by_a_gap()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand> { new MouseCommand { Action = MouseAction.DoubleClick, Button = MouseButton.Left, X = 5, Y = 5 } };

        await player.RunAsync(commands, new PlaybackOptions());

        var buttons = sim.Events.OfType<ButtonEvent>().ToList();
        Assert.Equal([true, false, true, false], buttons.Select(b => b.Down));
    }

    [Fact]
    public async Task Screen_mode_uses_the_raw_coordinates()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand> { new MouseCommand { Action = MouseAction.Move, X = 800, Y = 600, CoordinateMode = CoordinateMode.Screen } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(new MoveEvent(800, 600), sim.Events.Single());
    }

    [Fact]
    public async Task Relative_mode_offsets_from_the_current_cursor_position_and_chains()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        sim.CursorX = 100;
        sim.CursorY = 100;
        var commands = new List<MacroCommand>
        {
            new MouseCommand { Action = MouseAction.Move, X = 10, Y = -10, CoordinateMode = CoordinateMode.Relative },
            new MouseCommand { Action = MouseAction.Move, X = 5, Y = 5, CoordinateMode = CoordinateMode.Relative },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(110, 90), new MoveEvent(115, 95)], sim.Events);
    }

    [Fact]
    public async Task ActiveWindow_mode_offsets_by_the_window_position_and_activates_once_per_distinct_window()
    {
        var (player, sim, windows, _, _, _) = NewPlayer();
        nint handle = 42;
        windows.Windows[("Bloc-notes", null)] = handle;
        windows.Bounds[handle] = (100, 200);
        var commands = new List<MacroCommand>
        {
            new MouseCommand { Action = MouseAction.Move, X = 5, Y = 5, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "Bloc-notes" },
            new MouseCommand { Action = MouseAction.Move, X = 6, Y = 6, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "Bloc-notes" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(105, 205), new MoveEvent(106, 206)], sim.Events);
        Assert.Single(windows.Activations); // pas réactivée pour la 2e commande, même fenêtre.
    }

    [Fact]
    public async Task WindowNotFound_Ignore_skips_only_that_command()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand>
        {
            new MouseCommand { Action = MouseAction.Move, X = 1, Y = 1, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "Introuvable" },
            Move(2, 2),
        };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.Ignore });

        Assert.Equal([new MoveEvent(2, 2)], sim.Events);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task WindowNotFound_Stop_halts_the_whole_run()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new MouseCommand { Action = MouseAction.Move, X = 1, Y = 1, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "Introuvable" },
            Move(2, 2),
        };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.Stop });

        Assert.Empty(sim.Events);
    }

    [Fact]
    public async Task WindowNotFound_WaitAndRetry_finds_the_window_that_appears_mid_poll()
    {
        var sim = new FakeInputSimulator();
        var windows = new FakeWindowFinder();
        nint handle = 7;
        windows.Bounds[handle] = (0, 0);
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                windows.Windows[("App", null)] = handle; // apparaît juste avant le 2e sondage.
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(sim, windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new MouseCommand { X = 1, Y = 1, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "App" } };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.WaitAndRetry, WaitForWindowTimeoutMs = 10_000 });

        Assert.Equal(new MoveEvent(1, 1), sim.Events.OfType<MoveEvent>().Single());
        Assert.True(windows.FindWindowCallCount >= 2);
    }

    [Fact]
    public async Task WindowNotFound_WaitAndRetry_times_out_and_then_behaves_like_ignore()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new MouseCommand { X = 1, Y = 1, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "JamaisLà" },
            Move(9, 9),
        };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.WaitAndRetry, WaitForWindowTimeoutMs = 500 });

        Assert.Equal([new MoveEvent(9, 9)], sim.Events);
    }

    // ------------------------------------------------------------------------------------------------ Arrêt d'urgence

    [Fact]
    public async Task Emergency_stop_releases_a_mouse_button_left_pressed()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        using var cts = new CancellationTokenSource();
        player.CommandStarted += (_, i) =>
        {
            if (i == 1)
            {
                cts.Cancel(); // le Down a déjà été exécuté ; on annule juste avant le Up qui l'aurait relâché normalement.
            }
        };
        var commands = new List<MacroCommand>
        {
            new MouseCommand { Action = MouseAction.Down, Button = MouseButton.Left, X = 1, Y = 1 },
            new MouseCommand { Action = MouseAction.Up, Button = MouseButton.Left, X = 1, Y = 1 },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            player.RunAsync(commands, new PlaybackOptions(), cancellationToken: cts.Token));

        Assert.Contains(sim.Events, e => e is ButtonEvent { Button: MouseButton.Left, Down: false });
    }

    [Fact]
    public async Task Emergency_stop_releases_a_held_key()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        using var cts = new CancellationTokenSource();
        player.CommandStarted += (_, i) =>
        {
            if (i == 1)
            {
                cts.Cancel();
            }
        };
        var commands = new List<MacroCommand>
        {
            new KeyboardCommand { Action = KeyAction.Down, VirtualKey = 0x41, ScanCode = 0x1E },
            new KeyboardCommand { Action = KeyAction.Up, VirtualKey = 0x41, ScanCode = 0x1E },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            player.RunAsync(commands, new PlaybackOptions(), cancellationToken: cts.Token));

        Assert.Contains(sim.Events, e => e is KeyEvent { ScanCode: 0x1E, Down: false });
    }

    // ------------------------------------------------------------------------------------------------ Clavier

    [Fact]
    public async Task Holding_ctrl_across_two_keys_presses_it_once_and_releases_it_once()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new KeyboardCommand { Action = KeyAction.Down, VirtualKey = 0x41, ScanCode = 0x1E, Modifiers = KeyModifiers.Ctrl },
            new KeyboardCommand { Action = KeyAction.Down, VirtualKey = 0x42, ScanCode = 0x30, Modifiers = KeyModifiers.Ctrl },
            new KeyboardCommand { Action = KeyAction.Up, VirtualKey = 0x41, ScanCode = 0x1E, Modifiers = KeyModifiers.Ctrl },
            new KeyboardCommand { Action = KeyAction.Up, VirtualKey = 0x42, ScanCode = 0x30, Modifiers = KeyModifiers.Ctrl },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        var ctrlEvents = sim.Events.OfType<KeyEvent>().Where(e => e.ScanCode == 0x1D).ToList();
        Assert.Equal([true, false], ctrlEvents.Select(e => e.Down)); // pressé une seule fois, relâché une seule fois.
        Assert.True(sim.Events.IndexOf(new KeyEvent(0x11, 0x1D, false, true)) < sim.Events.IndexOf(new KeyEvent(0x41, 0x1E, false, true)));
        Assert.True(sim.Events.IndexOf(new KeyEvent(0x11, 0x1D, false, false)) > sim.Events.IndexOf(new KeyEvent(0x42, 0x30, false, false)));
    }

    [Fact]
    public async Task Press_with_modifiers_is_an_atomic_chord()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new KeyboardCommand { Action = KeyAction.Press, VirtualKey = 0x43, ScanCode = 0x2E, Modifiers = KeyModifiers.Ctrl },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(
        [
            new KeyEvent(0x11, 0x1D, false, true),
            new KeyEvent(0x43, 0x2E, false, true),
            new KeyEvent(0x43, 0x2E, false, false),
            new KeyEvent(0x11, 0x1D, false, false),
        ], sim.Events);
    }

    [Fact]
    public async Task Text_types_each_character_and_waits_between_but_not_after_the_last()
    {
        var (player, sim, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { new TextCommand { Text = "abc", CharacterDelayMs = 15 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new CharEvent('a'), new CharEvent('b'), new CharEvent('c')], sim.Events);
        Assert.Equal([15, 15], delays);
    }

    [Fact]
    public async Task Text_expands_var_date_and_clipboard_tokens_by_default()
    {
        var clipboard = new FakeClipboardService { Text = "X" };
        var clock = new DateTime(2026, 1, 2, 3, 4, 5);
        var (player, sim, _, _, _, _) = NewPlayer(clipboard: clipboard, clock: () => clock);
        var commands = new List<MacroCommand> { new ClipboardCommand { Action = ClipboardAction.ReadToVariable, VariableName = "v" }, new TextCommand { Text = "{var:v}{clipboard}" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new CharEvent('X'), new CharEvent('X')], sim.Events);
    }

    [Fact]
    public async Task Text_does_not_expand_tokens_when_ExpandTokens_is_false()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand> { new TextCommand { Text = "{var:x}", ExpandTokens = false } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("{var:x}", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Clipboard_copy_sets_the_clipboard_with_expanded_text()
    {
        var clipboard = new FakeClipboardService();
        var (player, _, _, _, _, _) = NewPlayer(clipboard: clipboard);
        var commands = new List<MacroCommand> { new ClipboardCommand { Action = ClipboardAction.Copy, Text = "hello" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("hello", clipboard.Text);
    }

    [Fact]
    public async Task Clipboard_paste_sends_ctrl_v()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand> { new ClipboardCommand { Action = ClipboardAction.Paste } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(
        [
            new KeyEvent(0x11, 0x1D, false, true),
            new KeyEvent(0x56, 0x2F, false, true),
            new KeyEvent(0x56, 0x2F, false, false),
            new KeyEvent(0x11, 0x1D, false, false),
        ], sim.Events);
    }

    [Fact]
    public async Task Clipboard_read_to_variable_stores_the_clipboard_text()
    {
        var clipboard = new FakeClipboardService { Text = "depuis le presse-papiers" };
        var (player, sim, _, _, _, _) = NewPlayer(clipboard: clipboard);
        var commands = new List<MacroCommand>
        {
            new ClipboardCommand { Action = ClipboardAction.ReadToVariable, VariableName = "v" },
            new TextCommand { Text = "{var:v}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("depuis le presse-papiers", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Launch_program_passes_through_to_the_launcher_with_expanded_tokens()
    {
        var launcher = new FakeProcessLauncher();
        var (player, _, _, _, _, _) = NewPlayer(launcher: launcher);
        var commands = new List<MacroCommand> { new LaunchCommand { Mode = LaunchMode.Program, Path = "notepad.exe", Arguments = "{var:f}", WorkingDirectory = @"C:\tmp" } };
        commands.Insert(0, new ClipboardCommand { Action = ClipboardAction.ReadToVariable, VariableName = "f" }); // laisse f vide, juste pour vérifier l'expansion ne plante pas.

        await player.RunAsync(commands, new PlaybackOptions());

        var call = Assert.Single(launcher.Calls);
        Assert.Equal("notepad.exe", call.Path);
        Assert.Equal(@"C:\tmp", call.WorkingDirectory);
        Assert.False(call.ShellExecute);
    }

    [Fact]
    public async Task Launch_shell_command_uses_shell_execute()
    {
        var launcher = new FakeProcessLauncher();
        var (player, _, _, _, _, _) = NewPlayer(launcher: launcher);
        var commands = new List<MacroCommand> { new LaunchCommand { Mode = LaunchMode.ShellCommand, Path = "dir" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(launcher.Calls.Single().ShellExecute);
    }

    [Fact]
    public async Task Launch_timeout_warns_but_continues()
    {
        var launcher = new FakeProcessLauncher { ExitCode = null };
        var (player, sim, _, _, _, _) = NewPlayer(launcher: launcher);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new LaunchCommand { Path = "x.exe", WaitForExit = true }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Launch_exception_warns_but_does_not_crash()
    {
        var launcher = new FakeProcessLauncher { ThrowOnLaunch = new InvalidOperationException("échec") };
        var (player, sim, _, _, _, _) = NewPlayer(launcher: launcher);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new LaunchCommand { Path = "x.exe" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task OpenUrl_launches_with_shell_execute_and_no_wait()
    {
        var launcher = new FakeProcessLauncher();
        var (player, _, _, _, _, _) = NewPlayer(launcher: launcher);
        var commands = new List<MacroCommand> { new OpenUrlCommand { Path = "https://example.test" } };

        await player.RunAsync(commands, new PlaybackOptions());

        var call = Assert.Single(launcher.Calls);
        Assert.Equal("https://example.test", call.Path);
        Assert.True(call.ShellExecute);
        Assert.False(call.WaitForExit);
    }

    [Theory]
    [InlineData(WindowAction.Minimize, "Minimize")]
    [InlineData(WindowAction.Maximize, "Maximize")]
    [InlineData(WindowAction.Restore, "Restore")]
    [InlineData(WindowAction.Close, "Close")]
    public async Task Window_action_dispatches_to_the_controller(WindowAction action, string expectedCallName)
    {
        var windowController = new FakeWindowController();
        var windows = new FakeWindowFinder();
        nint handle = 42;
        windows.Windows[("App", null)] = handle;
        var player = new MacroPlayer(new FakeInputSimulator(), windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), windowController, new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand> { new WindowCommand { Action = action, WindowTitle = "App" } };

        await player.RunAsync(commands, new PlaybackOptions());

        var call = Assert.Single(windowController.Calls);
        Assert.Equal(expectedCallName, call.Action);
        Assert.Equal(handle, call.Window);
    }

    [Fact]
    public async Task Window_activate_uses_the_window_finder_activation()
    {
        var windows = new FakeWindowFinder();
        nint handle = 7;
        windows.Windows[("App", null)] = handle;
        var player = new MacroPlayer(new FakeInputSimulator(), windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand> { new WindowCommand { Action = WindowAction.Activate, WindowTitle = "App" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(windows.Activations);
    }

    [Fact]
    public async Task Window_moveresize_passes_the_coordinates()
    {
        var windowController = new FakeWindowController();
        var windows = new FakeWindowFinder();
        nint handle = 3;
        windows.Windows[("App", null)] = handle;
        var player = new MacroPlayer(new FakeInputSimulator(), windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), windowController, new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand> { new WindowCommand { Action = WindowAction.MoveResize, WindowTitle = "App", X = 10, Y = 20, Width = 300, Height = 400 } };

        await player.RunAsync(commands, new PlaybackOptions());

        var call = Assert.Single(windowController.Calls);
        Assert.Equal(new FakeWindowController.Call("MoveResize", handle, 10, 20, 300, 400), call);
    }

    [Fact]
    public async Task Window_not_found_Ignore_warns_and_continues()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new WindowCommand { Action = WindowAction.Close, WindowTitle = "Introuvable" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.Ignore });

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Window_not_found_Stop_halts_the_run()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand> { new WindowCommand { Action = WindowAction.Close, WindowTitle = "Introuvable" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions { WindowNotFoundAction = WindowNotFoundAction.Stop });

        Assert.Empty(sim.Events);
    }

    // ------------------------------------------------------------------------------------------------ Pixel

    [Fact]
    public async Task Pixel_test_true_when_the_color_matches_and_stores_the_variable()
    {
        var pixels = new FakePixelReader();
        pixels.Colors[(5, 5)] = (255, 0, 0);
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), pixels, new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand>
        {
            new PixelCommand { Mode = PixelActionMode.Test, X = 5, Y = 5, ExpectedColorHex = "#FF0000", VariableName = "v" },
            new TextCommand { Text = "{var:v}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("1", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Pixel_test_false_when_the_color_does_not_match()
    {
        var pixels = new FakePixelReader();
        pixels.Colors[(5, 5)] = (0, 0, 0);
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), pixels, new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand>
        {
            new PixelCommand { Mode = PixelActionMode.Test, X = 5, Y = 5, ExpectedColorHex = "#FF0000", VariableName = "v" },
            new TextCommand { Text = "{var:v}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("0", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Pixel_wait_returns_as_soon_as_the_color_appears()
    {
        var pixels = new FakePixelReader();
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                pixels.Colors[(1, 1)] = (0, 255, 0);
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(new FakeInputSimulator(), new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), pixels, new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new PixelCommand { Mode = PixelActionMode.Wait, X = 1, Y = 1, ExpectedColorHex = "#00FF00", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(pollCount >= 2);
    }

    [Fact]
    public async Task Pixel_wait_times_out_and_warns()
    {
        var (player, _, _, _, _, _) = NewPlayer();
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new PixelCommand { Mode = PixelActionMode.Wait, X = 1, Y = 1, ExpectedColorHex = "#00FF00", TimeoutMs = 500 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
    }

    [Fact]
    public async Task Wait_PixelMatch_polls_until_the_color_appears()
    {
        var pixels = new FakePixelReader();
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                pixels.Colors[(2, 2)] = (1, 2, 3);
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(new FakeInputSimulator(), new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), pixels, new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.PixelMatch, PixelX = 2, PixelY = 2, PixelColorHex = "#010203", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(pollCount >= 2);
    }

    [Fact]
    public async Task Wait_command_uses_the_fixed_duration_when_there_is_no_randomness()
    {
        var (player, _, _, _, delays, _) = NewPlayer();
        var commands = new List<MacroCommand> { new WaitCommand { DurationMs = 400 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([400], delays);
    }

    [Fact]
    public async Task Wait_command_adds_the_random_extra_via_the_injected_random_source()
    {
        var (player, _, _, _, delays, _) = NewPlayer(random: new MinRandom()); // MinRandom renvoie toujours le minimum (0 ajouté).
        var commands = new List<MacroCommand> { new WaitCommand { DurationMs = 400, RandomExtraMs = 300 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([400], delays); // minimum de la plage [400, 700] avec ce générateur factice.
    }

    [Fact]
    public async Task Wait_WindowAppears_returns_as_soon_as_the_window_is_found()
    {
        var sim = new FakeInputSimulator();
        var windows = new FakeWindowFinder();
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                windows.Windows[("App", null)] = 7;
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(sim, windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.WindowAppears, WindowTitle = "App", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(windows.FindWindowCallCount >= 2);
    }

    [Fact]
    public async Task Wait_WindowAppears_times_out_and_warns()
    {
        var (player, _, _, _, _, _) = NewPlayer();
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.WindowAppears, WindowTitle = "JamaisLà", TimeoutMs = 500 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
    }

    [Fact]
    public async Task Wait_WindowDisappears_returns_once_the_window_is_gone()
    {
        var sim = new FakeInputSimulator();
        var windows = new FakeWindowFinder();
        windows.Windows[("App", null)] = 7;
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                windows.Windows.Remove(("App", null));
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(sim, windows, new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.WindowDisappears, WindowTitle = "App", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(windows.FindWindowCallCount >= 2);
    }

    [Fact]
    public async Task Wait_KeyPress_waits_for_the_key_waiter()
    {
        var keyWaiter = new FakeKeyWaiter { Result = true };
        var (player, _, _, _, _, _) = NewPlayer(keyWaiter: keyWaiter);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.KeyPress, VirtualKey = 0x41, TimeoutMs = 5000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new FakeKeyWaiter.Call(0x41, 5000)], keyWaiter.Calls);
    }

    [Fact]
    public async Task Wait_KeyPress_timeout_warns_but_continues()
    {
        var keyWaiter = new FakeKeyWaiter { Result = false };
        var (player, sim, _, _, _, _) = NewPlayer(keyWaiter: keyWaiter);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.KeyPress }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    // ------------------------------------------------------------------------------------------------ Image

    [Fact]
    public async Task ImageSearch_found_stores_variables_and_clicks()
    {
        var images = new FakeImageSearcher { Result = (10, 20) };
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), images, new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand>
        {
            new ImageSearchCommand { TemplatePngBase64 = "AAAA", ClickIfFound = true, ClickButton = MouseButton.Right, FoundXVariable = "fx", FoundYVariable = "fy" },
            new TextCommand { Text = "{var:fx},{var:fy}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Contains(sim.Events, e => e is MoveEvent(10, 20));
        Assert.Contains(sim.Events, e => e is ButtonEvent { Button: MouseButton.Right, Down: true });
        Assert.Equal("10,20", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task ImageSearch_found_with_DoubleClick_clicks_twice()
    {
        var images = new FakeImageSearcher { Result = (10, 20) };
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), images, new FakeFileLineSource(), new FakeMacroLoader());
        var commands = new List<MacroCommand>
        {
            new ImageSearchCommand { TemplatePngBase64 = "AAAA", ClickIfFound = true, DoubleClick = true },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(2, sim.Events.Count(e => e is ButtonEvent { Button: MouseButton.Left, Down: true }));
        Assert.Equal(2, sim.Events.Count(e => e is ButtonEvent { Button: MouseButton.Left, Down: false }));
    }

    [Fact]
    public async Task ImageSearch_not_found_does_not_click_or_set_variables()
    {
        var images = new FakeImageSearcher { Result = null };
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), images, new FakeFileLineSource(), new FakeMacroLoader());
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new ImageSearchCommand { TemplatePngBase64 = "AAAA", ClickIfFound = true, TimeoutMs = 0 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Empty(sim.Events);
        Assert.Contains("Recherche d'image : modèle non trouvé.", warnings);
    }

    [Fact]
    public async Task ImageSearch_waits_until_found_when_a_timeout_is_set()
    {
        var images = new FakeImageSearcher();
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                images.Result = (5, 5);
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(new FakeInputSimulator(), new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), images, new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new ImageSearchCommand { TemplatePngBase64 = "AAAA", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(pollCount >= 2);
    }

    [Fact]
    public async Task Wait_ImageFound_polls_until_the_image_appears()
    {
        var images = new FakeImageSearcher();
        var pollCount = 0;
        Task Delay(int ms, CancellationToken ct)
        {
            pollCount++;
            if (pollCount == 2)
            {
                images.Result = (1, 1);
            }

            return Task.CompletedTask;
        }

        var player = new MacroPlayer(new FakeInputSimulator(), new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), images, new FakeFileLineSource(), new FakeMacroLoader(), Delay);
        var commands = new List<MacroCommand> { new WaitCommand { Mode = WaitMode.ImageFound, ImageTemplatePngBase64 = "AAAA", TimeoutMs = 10_000 } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(pollCount >= 2);
    }

    // ------------------------------------------------------------------------------------------------ Sons / messages

    [Fact]
    public async Task Sound_plays_the_file()
    {
        var soundPlayer = new FakeSoundPlayer();
        var (player, _, _, _, _, _) = NewPlayer(soundPlayer: soundPlayer);
        var commands = new List<MacroCommand> { new SoundCommand { FilePath = "ding.wav", WaitForCompletion = true } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new FakeSoundPlayer.Call("ding.wav", true)], soundPlayer.Calls);
    }

    [Fact]
    public async Task Sound_exception_warns_but_does_not_crash()
    {
        var soundPlayer = new FakeSoundPlayer { ThrowOnPlay = new InvalidOperationException("échec") };
        var (player, sim, _, _, _, _) = NewPlayer(soundPlayer: soundPlayer);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new SoundCommand { FilePath = "x.wav" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Message_shows_with_expanded_text_and_stores_no_variable_by_default()
    {
        var messageBox = new FakeMessageBoxService();
        var (player, _, _, _, _, _) = NewPlayer(messageBox: messageBox);
        var commands = new List<MacroCommand> { new MessageCommand { Title = "Titre", Text = "{var:x}", MessageKind = MessageBoxKind.Warning } };
        // pas de variable définie -> {var:x} s'expanse en "".

        await player.RunAsync(commands, new PlaybackOptions());

        var call = Assert.Single(messageBox.Calls);
        Assert.Equal("Titre", call.Title);
        Assert.Equal("", call.Text);
        Assert.Equal(MessageBoxKind.Warning, call.Kind);
    }

    [Fact]
    public async Task Message_OkCancel_stores_the_result_variable()
    {
        var messageBox = new FakeMessageBoxService { Result = V0XMacroRecorder.Core.Abstractions.MessageBoxResult.Cancel };
        var (player, sim, _, _, _, _) = NewPlayer(messageBox: messageBox);
        var commands = new List<MacroCommand>
        {
            new MessageCommand { Title = "T", Text = "x", MessageKind = MessageBoxKind.OkCancel, ResultVariableName = "r" },
            new TextCommand { Text = "{var:r}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("cancel", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    // ------------------------------------------------------------------------------------------------ Script C#

    [Fact]
    public async Task Script_receives_the_code_and_timeout_then_continues()
    {
        var scriptRunner = new FakeScriptRunner();
        var (player, sim, _, _, _, _) = NewPlayer(scriptRunner: scriptRunner);
        var commands = new List<MacroCommand> { new ScriptCommand { Code = "Mouse.Click();", TimeoutSeconds = 5 }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([("Mouse.Click();", 5000)], scriptRunner.Calls);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events); // la commande suivante s'exécute bien : Script ne stoppe jamais la macro lui-même.
    }

    [Fact]
    public async Task Script_failure_warns_but_does_not_stop_the_macro()
    {
        var scriptRunner = new FakeScriptRunner { Result = new ScriptRunResult(false, "erreur de compilation") };
        var (player, sim, _, _, _, _) = NewPlayer(scriptRunner: scriptRunner);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new ScriptCommand { Code = "?!" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Contains("erreur de compilation", warnings[0]);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Script_without_a_configured_runner_warns_and_is_skipped()
    {
        var (player, _, _, _, _, _) = NewPlayer(); // scriptRunner: null par défaut.
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new ScriptCommand { Code = "Mouse.Click();" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
    }

    [Fact]
    public async Task Script_globals_share_the_real_simulator_and_variable_store()
    {
        var scriptRunner = new FakeScriptRunner
        {
            OnRun = globals =>
            {
                globals.Mouse.MoveTo(42, 43);
                globals.Vars.Set("fromScript", "hello");
            },
        };
        var (player, sim, _, _, _, _) = NewPlayer(scriptRunner: scriptRunner);
        var commands = new List<MacroCommand> { new ScriptCommand(), new TextCommand { Text = "{var:fromScript}" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(42, 43)], sim.Events.OfType<MoveEvent>());
        Assert.Equal("hello", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    // ------------------------------------------------------------------------------------------------ Contrôle de flux : Si

    private static IfCommand IfWindowExists(string title, bool negate = false) => new()
    {
        Condition = new ConditionSpec { Kind = ConditionKind.WindowExists, WindowTitle = title },
        Negate = negate,
    };

    [Fact]
    public async Task If_true_executes_the_body_and_skips_a_trailing_else()
    {
        var (player, sim, windows, _, _, _) = NewPlayer();
        windows.Windows[("App", null)] = 1;
        var commands = new List<MacroCommand>
        {
            IfWindowExists("App"),
            Move(1, 1),
            new ElseCommand(),
            Move(2, 2),
            new EndIfCommand(),
            Move(3, 3),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(1, 1), new MoveEvent(3, 3)], sim.Events);
    }

    [Fact]
    public async Task If_false_without_else_jumps_past_endif()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            IfWindowExists("Introuvable"),
            Move(1, 1),
            new EndIfCommand(),
            Move(2, 2),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(2, 2)], sim.Events);
    }

    [Fact]
    public async Task If_false_with_else_executes_the_else_body()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            IfWindowExists("Introuvable"),
            Move(1, 1),
            new ElseCommand(),
            Move(2, 2),
            new EndIfCommand(),
            Move(3, 3),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(2, 2), new MoveEvent(3, 3)], sim.Events);
    }

    [Fact]
    public async Task Negate_inverts_the_condition()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            IfWindowExists("Introuvable", negate: true),
            Move(1, 1),
            new EndIfCommand(),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Nested_if_evaluates_independently()
    {
        var (player, sim, windows, _, _, _) = NewPlayer();
        windows.Windows[("Outer", null)] = 1;
        // Outer vrai -> entre ; Inner faux -> saute son corps ; puis marque(2,2) après le if interne.
        var commands = new List<MacroCommand>
        {
            IfWindowExists("Outer"),
            IfWindowExists("Inner"),
            Move(1, 1),
            new EndIfCommand(),
            Move(2, 2),
            new EndIfCommand(),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(2, 2)], sim.Events);
    }

    // ------------------------------------------------------------------------------------------------ Boucles

    [Fact]
    public async Task RepeatCount_loop_runs_the_body_exactly_n_times()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new LoopCommand { Mode = LoopMode.RepeatCount, RepeatCount = 3 },
            Move(1, 1),
            new EndLoopCommand(),
            Move(9, 9),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(3, sim.Events.OfType<MoveEvent>().Count(e => e.X == 1 && e.Y == 1));
        Assert.Single(sim.Events.OfType<MoveEvent>().Where(e => e.X == 9 && e.Y == 9));
    }

    [Fact]
    public async Task Nested_repeat_loops_reset_the_inner_counter_each_outer_iteration()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new LoopCommand { Mode = LoopMode.RepeatCount, RepeatCount = 2 }, // outer, index 0
            new LoopCommand { Mode = LoopMode.RepeatCount, RepeatCount = 3 }, // inner, index 1
            Move(1, 1),
            new EndLoopCommand(), // ferme inner, index 3
            new EndLoopCommand(), // ferme outer, index 4
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(6, sim.Events.OfType<MoveEvent>().Count());
    }

    [Fact]
    public async Task While_loop_runs_until_the_condition_becomes_false()
    {
        var (player, sim, windows, _, _, _) = NewPlayer();
        windows.Windows[("App", null)] = 1;
        var commands = new List<MacroCommand>
        {
            new LoopCommand { Mode = LoopMode.While, WhileCondition = new ConditionSpec { Kind = ConditionKind.WindowExists, WindowTitle = "App" } },
            Move(1, 1),
            new VariableCommand { Mode = VariableMode.Increment, Name = "n" }, // sans lien avec la condition, juste pour compter les passages.
            new EndLoopCommand(),
        };
        var iterations = 0;
        player.CommandStarted += (_, i) =>
        {
            if (i == 1 && ++iterations == 3)
            {
                windows.Windows.Remove(("App", null)); // la condition devient fausse après 3 passages.
            }
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal(3, sim.Events.OfType<MoveEvent>().Count());
    }

    [Fact]
    public async Task ForEachLine_sets_the_variable_for_every_line_in_order()
    {
        var files = new FakeFileLineSource();
        files.Files["lines.txt"] = ["a", "b", "c"];
        var (player, sim, _, _, _, _) = NewPlayer(fileLineSource: files);
        var commands = new List<MacroCommand>
        {
            new LoopCommand { Mode = LoopMode.ForEachLine, FilePath = "lines.txt", LineVariableName = "line" },
            new TextCommand { Text = "{var:line}" },
            new EndLoopCommand(),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("abc", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    // ------------------------------------------------------------------------------------------------ Variables

    [Fact]
    public async Task Variable_set_stores_the_expanded_value()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new VariableCommand { Mode = VariableMode.Set, Name = "x", Value = "hello" },
            new TextCommand { Text = "{var:x}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("hello", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Variable_increment_defaults_to_one_and_accumulates()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new VariableCommand { Mode = VariableMode.Increment, Name = "n" },
            new VariableCommand { Mode = VariableMode.Increment, Name = "n" },
            new VariableCommand { Mode = VariableMode.Increment, Name = "n", Value = "5" },
            new TextCommand { Text = "{var:n}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("7", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Variable_calculate_evaluates_an_expression_with_variable_tokens()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new VariableCommand { Mode = VariableMode.Set, Name = "a", Value = "3" },
            new VariableCommand { Mode = VariableMode.Calculate, Name = "result", Value = "{var:a} * 2 + 1" },
            new TextCommand { Text = "{var:result}" },
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("7", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    // ------------------------------------------------------------------------------------------------ Étiquettes / Aller à / Arrêter / Pause / Appeler

    [Fact]
    public async Task Goto_jumps_forward_over_intervening_commands()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new GotoCommand { TargetLabel = "fin" },
            Move(1, 1), // sauté.
            new LabelCommand { Name = "fin" },
            Move(2, 2),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(2, 2)], sim.Events);
    }

    [Fact]
    public async Task Goto_jumps_backward_and_re_executes()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new LabelCommand { Name = "debut" },
            new VariableCommand { Mode = VariableMode.Increment, Name = "n" },
            Move(1, 1),
            new IfCommand { Condition = new ConditionSpec { Kind = ConditionKind.VariableCompare, VariableName = "n", ComparisonOperator = ComparisonOperator.LessThan, ComparisonValue = "3" } },
            new GotoCommand { TargetLabel = "debut" },
            new EndIfCommand(),
        };

        await player.RunAsync(commands, new PlaybackOptions());

        // Move s'exécute à n=1, n=2 et n=3 (le Goto vers « debut » a lieu après le Move, donc la 3e passe
        // exécute bien Move avant que la condition n<3 devienne fausse et sorte de la boucle).
        Assert.Equal(3, sim.Events.OfType<MoveEvent>().Count());
    }

    [Fact]
    public async Task Goto_unknown_label_warns_and_continues()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new GotoCommand { TargetLabel = "inconnue" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Stop_command_halts_immediately_and_still_releases_held_keys()
    {
        var (player, sim, _, _, _, _) = NewPlayer();
        var commands = new List<MacroCommand>
        {
            new KeyboardCommand { Action = KeyAction.Down, VirtualKey = 0x41, ScanCode = 0x1E },
            new StopCommand(),
            new KeyboardCommand { Action = KeyAction.Up, VirtualKey = 0x41, ScanCode = 0x1E }, // jamais atteint.
        };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Contains(sim.Events, e => e is KeyEvent { ScanCode: 0x1E, Down: false }); // relâché par ReleaseEverything malgré l'arrêt.
    }

    [Fact]
    public async Task Pause_command_waits_for_the_key_waiter_then_continues()
    {
        var keyWaiter = new FakeKeyWaiter { Result = true };
        var (player, sim, _, _, _, _) = NewPlayer(keyWaiter: keyWaiter);
        var commands = new List<MacroCommand> { new PauseCommand { VirtualKey = 0x41, TimeoutMs = 5000 }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new FakeKeyWaiter.Call(0x41, 5000)], keyWaiter.Calls);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Call_executes_the_called_macro_commands_inline()
    {
        var macros = new FakeMacroLoader();
        macros.Macros["sub.v0xmacro"] = new Macro { Commands = [Move(9, 9)] };
        var (player, sim, _, _, _, _) = NewPlayer(macroLoader: macros);
        var commands = new List<MacroCommand> { Move(1, 1), new CallCommand { MacroFilePath = "sub.v0xmacro" }, Move(2, 2) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(1, 1), new MoveEvent(9, 9), new MoveEvent(2, 2)], sim.Events);
    }

    [Fact]
    public async Task Stop_inside_a_called_macro_does_not_stop_the_caller()
    {
        var macros = new FakeMacroLoader();
        macros.Macros["sub.v0xmacro"] = new Macro { Commands = [Move(9, 9), new StopCommand(), Move(8, 8)] };
        var (player, sim, _, _, _, _) = NewPlayer(macroLoader: macros);
        var commands = new List<MacroCommand> { new CallCommand { MacroFilePath = "sub.v0xmacro" }, Move(2, 2) };

        await player.RunAsync(commands, new PlaybackOptions());

        // Move(8,8) jamais atteint (Stop coupe la macro appelée), mais Move(2,2) après Call s'exécute (sémantique sous-routine).
        Assert.Equal([new MoveEvent(9, 9), new MoveEvent(2, 2)], sim.Events);
    }

    [Fact]
    public async Task Call_missing_macro_warns_and_continues()
    {
        var (player, sim, _, _, _, _) = NewPlayer(macroLoader: new FakeMacroLoader());
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new CallCommand { MacroFilePath = "introuvable.v0xmacro" }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    [Fact]
    public async Task Call_direct_self_recursion_is_detected_and_does_not_hang()
    {
        var macros = new FakeMacroLoader();
        macros.Macros["self.v0xmacro"] = new Macro { Commands = [Move(1, 1), new CallCommand { MacroFilePath = "self.v0xmacro" }] };
        var (player, sim, _, _, _, _) = NewPlayer(macroLoader: macros);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new CallCommand { MacroFilePath = "self.v0xmacro" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(sim.Events.OfType<MoveEvent>()); // le corps s'exécute une fois, puis l'auto-appel est détecté et ignoré.
        Assert.Contains(warnings, w => w.Contains("récursif", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Call_chain_exceeding_max_depth_stops_deepening_without_crashing()
    {
        var macros = new FakeMacroLoader();
        const int chainLength = 20; // au-delà de MaxCallDepth (16).
        for (var i = 0; i < chainLength; i++)
        {
            var next = i + 1 < chainLength ? $"level{i + 1}.v0xmacro" : null;
            macros.Macros[$"level{i}.v0xmacro"] = new Macro
            {
                Commands = next is null ? [Move(i, i)] : [Move(i, i), new CallCommand { MacroFilePath = next }],
            };
        }

        var (player, sim, _, _, _, _) = NewPlayer(macroLoader: macros);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new CallCommand { MacroFilePath = "level0.v0xmacro" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.True(sim.Events.OfType<MoveEvent>().Count() <= 17); // s'arrête bien avant les 20 niveaux (jamais de dépassement de pile).
        Assert.Contains(warnings, w => w.Contains("profondeur", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------------------------------ Pause / pas à pas

    [Fact]
    public async Task Pause_blocks_progress_until_Resume_is_called()
    {
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader()); // horloge réelle : nécessaire pour laisser le test s'intercaler.
        var startedIndices = new List<int>();
        player.CommandStarted += (_, i) =>
        {
            startedIndices.Add(i);
            if (i == 0)
            {
                player.Pause();
            }
        };
        var commands = new List<MacroCommand> { Move(0, 0), Move(1, 1) };

        var run = player.RunAsync(commands, new PlaybackOptions());
        await Task.Delay(200); // laisse largement le temps à la commande 0 de s'exécuter et à la pause de prendre effet.

        Assert.Equal([0], startedIndices);
        Assert.True(player.IsPaused);

        player.Resume();
        await run;

        Assert.Equal([0, 1], startedIndices);
    }

    [Fact]
    public async Task StepMode_pauses_before_every_command()
    {
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var startedIndices = new List<int>();
        player.CommandStarted += (_, i) => startedIndices.Add(i);
        var commands = new List<MacroCommand> { Move(0, 0), Move(1, 1), Move(2, 2) };

        var run = player.RunAsync(commands, new PlaybackOptions { StepMode = true });
        await Task.Delay(100);
        Assert.Empty(startedIndices); // en pause avant même la 1re commande.

        player.Resume();
        await Task.Delay(100);
        Assert.Equal([0], startedIndices);

        player.Resume();
        await Task.Delay(100);
        Assert.Equal([0, 1], startedIndices);

        player.Resume();
        await run;
        Assert.Equal([0, 1, 2], startedIndices);
    }

    [Fact]
    public async Task Breakpoint_pauses_only_before_the_marked_index()
    {
        var sim = new FakeInputSimulator();
        var player = new MacroPlayer(sim, new FakeWindowFinder(), new FakeElevationService(), new FakeKeyWaiter(), new FakeClipboardService(), new FakeProcessLauncher(), new FakeWindowController(), new FakePixelReader(), new FakeSoundPlayer(), new FakeMessageBoxService(), new FakeImageSearcher(), new FakeFileLineSource(), new FakeMacroLoader());
        var startedIndices = new List<int>();
        player.CommandStarted += (_, i) => startedIndices.Add(i);
        var commands = new List<MacroCommand> { Move(0, 0), Move(1, 1), Move(2, 2) };

        var run = player.RunAsync(commands, new PlaybackOptions(), breakpoints: new HashSet<int> { 1 });
        await Task.Delay(100);
        Assert.Equal([0], startedIndices); // s'arrête avant la commande 1.

        player.Resume();
        await run;
        Assert.Equal([0, 1, 2], startedIndices);
    }

    // ------------------------------------------------------------------------------------------------ Session verrouillée (étape 8)

    [Fact]
    public async Task Locked_session_warns_and_stops_before_the_first_command()
    {
        var sessionLock = new FakeSessionLockService { Locked = true };
        var (player, sim, _, _, _, _) = NewPlayer(sessionLock: sessionLock);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Single(warnings);
        Assert.Empty(sim.Events); // aucune entrée injectée à l'aveugle.
    }

    [Fact]
    public async Task Unlocked_session_plays_normally()
    {
        var sessionLock = new FakeSessionLockService { Locked = false };
        var (player, sim, _, _, _, _) = NewPlayer(sessionLock: sessionLock);
        var commands = new List<MacroCommand> { Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal([new MoveEvent(1, 1)], sim.Events);
    }

    // ------------------------------------------------------------------------------------------------ Saisie protégée (étape 8)

    [Fact]
    public async Task Prompt_mode_types_the_secret_returned_by_the_prompter()
    {
        var prompter = new FakeSecureInputPrompter { NextResult = "s3cret" };
        var (player, sim, _, _, _, _) = NewPlayer(securePrompter: prompter);
        var commands = new List<MacroCommand> { new SecureInputCommand { PromptAtPlayback = true, PromptLabel = "Mot de passe du site" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("s3cret", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
        Assert.Equal([("Saisie protégée", "Mot de passe du site")], prompter.Calls);
    }

    [Fact]
    public async Task Prompt_mode_cancelled_by_the_user_types_nothing_and_warns()
    {
        var prompter = new FakeSecureInputPrompter { NextResult = null };
        var (player, sim, _, _, _, _) = NewPlayer(securePrompter: prompter);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new SecureInputCommand { PromptAtPlayback = true }, Move(1, 1) };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Empty(sim.Events.OfType<CharEvent>());
        Assert.Single(warnings);
        Assert.Equal([new MoveEvent(1, 1)], sim.Events); // la commande suivante s'exécute quand même.
    }

    [Fact]
    public async Task Stored_secret_mode_decrypts_and_types_it()
    {
        var protector = new FakeDataProtector();
        var encrypted = protector.Protect("hunter2");
        var (player, sim, _, _, _, _) = NewPlayer(dataProtector: protector);
        var commands = new List<MacroCommand> { new SecureInputCommand { PromptAtPlayback = false, ProtectedValueBase64 = encrypted } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Equal("hunter2", string.Concat(sim.Events.OfType<CharEvent>().Select(e => e.Character)));
    }

    [Fact]
    public async Task Stored_secret_that_fails_to_decrypt_warns_without_leaking_details()
    {
        var protector = new FakeDataProtector { ThrowOnUnprotect = true };
        var (player, sim, _, _, _, _) = NewPlayer(dataProtector: protector);
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new SecureInputCommand { PromptAtPlayback = false, ProtectedValueBase64 = "garbage" } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Empty(sim.Events.OfType<CharEvent>());
        Assert.Single(warnings);
        Assert.DoesNotContain("Simulated unprotect failure", warnings[0]);
    }

    [Fact]
    public async Task Secure_input_without_configured_services_warns_and_is_skipped()
    {
        var (player, sim, _, _, _, _) = NewPlayer(); // ni prompter ni dataProtector configurés.
        var warnings = new List<string>();
        player.Warning += (_, w) => warnings.Add(w);
        var commands = new List<MacroCommand> { new SecureInputCommand { PromptAtPlayback = true } };

        await player.RunAsync(commands, new PlaybackOptions());

        Assert.Empty(sim.Events.OfType<CharEvent>());
        Assert.Single(warnings);
    }
}
