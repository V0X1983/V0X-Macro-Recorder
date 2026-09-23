using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Tests;

public sealed class RecordingSessionTests
{
    private static RawMouseEvent Move(int x, int y, long t, bool injected = false) =>
        new(RawMouseKind.Move, MouseButton.Left, x, y, 0, t, injected);

    private static RawMouseEvent Down(MouseButton button, int x, int y, long t, bool injected = false) =>
        new(RawMouseKind.Down, button, x, y, 0, t, injected);

    private static RawMouseEvent Up(MouseButton button, int x, int y, long t, bool injected = false) =>
        new(RawMouseKind.Up, button, x, y, 0, t, injected);

    private static RawMouseEvent Wheel(int delta, int x, int y, long t) =>
        new(RawMouseKind.Wheel, MouseButton.Left, x, y, delta, t, false);

    private static RawKeyEvent Key(int vk, bool down, long t, int scanCode = 0, char? character = null, bool injected = false) =>
        new(vk, scanCode, false, down, t, injected, character);

    private static (RecordingSession Session, List<MacroCommand> Commands) NewSession(RecordingOptions? options = null,
        Func<ActiveWindowInfo?>? activeWindow = null, Func<bool>? sensitive = null)
    {
        var session = new RecordingSession(options ?? new RecordingOptions(), activeWindow, sensitive);
        var commands = new List<MacroCommand>();
        session.CommandProduced += (_, c) => commands.Add(c);
        return (session, commands);
    }

    // ------------------------------------------------------------------------------------------------ Souris : déplacement

    [Fact]
    public void Consecutive_moves_merge_into_one_command_in_ClicksAndEndpoints_mode()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Move(10, 10, 0));
        session.HandleMouse(Move(20, 20, 50));
        session.HandleMouse(Move(30, 30, 100));
        session.HandleMouse(Down(MouseButton.Left, 99, 99, 150));

        var move = Assert.Single(commands.OfType<MouseCommand>(), c => c.Action == MouseAction.Move);
        Assert.Equal(30, move.X);
        Assert.Equal(30, move.Y);
        Assert.Equal(0, move.DelayMs); // ancré sur le premier échantillon du segment (t=0), 1re commande.
    }

    [Fact]
    public void FullPath_mode_emits_one_command_per_sample()
    {
        var options = new RecordingOptions { MouseSampling = MouseSamplingMode.FullPath };
        var (session, commands) = NewSession(options);

        session.HandleMouse(Move(10, 10, 0));
        session.HandleMouse(Move(20, 20, 50));
        session.HandleMouse(Move(30, 30, 100));

        Assert.Equal(3, commands.Count);
        Assert.Equal([10, 20, 30], commands.Cast<MouseCommand>().Select(c => c.X));
    }

    [Fact]
    public void Move_is_suppressed_when_the_following_click_lands_on_the_same_spot()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Move(50, 50, 0));
        session.HandleMouse(Down(MouseButton.Left, 50, 50, 10));
        session.HandleMouse(Up(MouseButton.Left, 50, 50, 20));

        Assert.DoesNotContain(commands, c => c is MouseCommand { Action: MouseAction.Move });
        Assert.Equal(2, commands.Count); // Down + Up seulement.
    }

    [Fact]
    public void Move_is_kept_when_the_following_click_lands_elsewhere()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Move(50, 50, 0));
        session.HandleMouse(Down(MouseButton.Left, 80, 80, 10));

        Assert.Contains(commands, c => c is MouseCommand { Action: MouseAction.Move, X: 50, Y: 50 });
    }

    // ------------------------------------------------------------------------------------------------ Souris : boutons

    [Fact]
    public void Click_produces_a_literal_Down_then_Up_pair_with_correct_delays()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Down(MouseButton.Right, 12, 34, 100));
        session.HandleMouse(Up(MouseButton.Right, 12, 34, 140));

        Assert.Collection(commands,
            c => { var m = Assert.IsType<MouseCommand>(c); Assert.Equal(MouseAction.Down, m.Action); Assert.Equal(MouseButton.Right, m.Button); Assert.Equal(0, m.DelayMs); },
            c => { var m = Assert.IsType<MouseCommand>(c); Assert.Equal(MouseAction.Up, m.Action); Assert.Equal(40, m.DelayMs); });
    }

    [Fact]
    public void Drag_keeps_down_and_up_at_their_own_positions()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Down(MouseButton.Left, 0, 0, 0));
        session.HandleMouse(Move(50, 50, 10));
        session.HandleMouse(Up(MouseButton.Left, 100, 100, 20));

        var down = Assert.IsType<MouseCommand>(commands[0]);
        Assert.Equal(0, down.X);
        var move = Assert.IsType<MouseCommand>(commands[1]);
        Assert.Equal(MouseAction.Move, move.Action);
        Assert.Equal(50, move.X);
        var up = Assert.IsType<MouseCommand>(commands[2]);
        Assert.Equal(100, up.X);
    }

    [Fact]
    public void Injected_mouse_events_are_ignored()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Down(MouseButton.Left, 1, 1, 0, injected: true));
        session.HandleMouse(Up(MouseButton.Left, 1, 1, 10, injected: true));

        Assert.Empty(commands);
    }

    [Fact]
    public void CaptureMouse_false_ignores_all_mouse_events()
    {
        var (session, commands) = NewSession(new RecordingOptions { CaptureMouse = false });

        session.HandleMouse(Down(MouseButton.Left, 1, 1, 0));
        session.HandleMouse(Up(MouseButton.Left, 1, 1, 10));

        Assert.Empty(commands);
    }

    // ------------------------------------------------------------------------------------------------ Souris : molette

    [Fact]
    public void Consecutive_same_direction_wheel_ticks_merge()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Wheel(1, 5, 5, 0));
        session.HandleMouse(Wheel(1, 5, 5, 50));
        session.HandleMouse(Wheel(1, 5, 5, 90));
        session.HandleMouse(Down(MouseButton.Left, 5, 5, 200)); // déclenche le vidage.

        var wheel = Assert.Single(commands.OfType<MouseCommand>(), c => c.Action == MouseAction.Wheel);
        Assert.Equal(3, wheel.WheelDelta);
    }

    [Fact]
    public void Wheel_direction_change_flushes_the_previous_merge()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Wheel(1, 5, 5, 0));
        session.HandleMouse(Wheel(-1, 5, 5, 50));
        session.Flush(60);

        var wheels = commands.OfType<MouseCommand>().Where(c => c.Action == MouseAction.Wheel).ToList();
        Assert.Equal(2, wheels.Count);
        Assert.Equal(1, wheels[0].WheelDelta);
        Assert.Equal(-1, wheels[1].WheelDelta);
    }

    [Fact]
    public void Wheel_merge_times_out_after_the_merge_window()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Wheel(1, 5, 5, 0));
        // Le second tick, hors fenêtre de fusion, vide d'abord le premier (ExpireStaleState) avant de démarrer le sien.
        session.HandleMouse(Wheel(1, 5, 5, 1000));
        session.Flush(1010);

        var wheels = commands.OfType<MouseCommand>().Where(c => c.Action == MouseAction.Wheel).ToList();
        Assert.Equal(2, wheels.Count);
    }

    // ------------------------------------------------------------------------------------------------ Repères de coordonnées

    [Fact]
    public void ActiveWindow_mode_uses_the_provided_window_bounds_and_title()
    {
        var options = new RecordingOptions { CoordinateMode = CoordinateMode.ActiveWindow };
        var (session, commands) = NewSession(options, activeWindow: () => new ActiveWindowInfo("Bloc-notes", "Notepad", 100, 200));

        session.HandleMouse(Down(MouseButton.Left, 150, 250, 0));

        var m = Assert.IsType<MouseCommand>(Assert.Single(commands));
        Assert.Equal(CoordinateMode.ActiveWindow, m.CoordinateMode);
        Assert.Equal(50, m.X);
        Assert.Equal(50, m.Y);
        Assert.Equal("Bloc-notes", m.WindowTitle);
        Assert.Equal("Notepad", m.WindowClassName);
    }

    [Fact]
    public void ActiveWindow_mode_falls_back_to_screen_when_no_window_is_resolved()
    {
        var options = new RecordingOptions { CoordinateMode = CoordinateMode.ActiveWindow };
        var (session, commands) = NewSession(options, activeWindow: () => null);

        session.HandleMouse(Down(MouseButton.Left, 150, 250, 0));

        var m = Assert.IsType<MouseCommand>(Assert.Single(commands));
        Assert.Equal(CoordinateMode.Screen, m.CoordinateMode);
        Assert.Equal(150, m.X);
        Assert.Null(m.WindowTitle);
    }

    [Fact]
    public void Relative_mode_computes_deltas_from_the_previous_position()
    {
        var options = new RecordingOptions { CoordinateMode = CoordinateMode.Relative };
        var (session, commands) = NewSession(options);

        session.HandleMouse(Down(MouseButton.Left, 100, 100, 0));
        session.HandleMouse(Up(MouseButton.Left, 130, 90, 10));

        var down = Assert.IsType<MouseCommand>(commands[0]);
        Assert.Equal(0, down.X); // pas de position antérieure connue.
        Assert.Equal(0, down.Y);
        var up = Assert.IsType<MouseCommand>(commands[1]);
        Assert.Equal(30, up.X);
        Assert.Equal(-10, up.Y);
    }

    // ------------------------------------------------------------------------------------------------ Clavier : modificateurs

    [Fact]
    public void Modifier_pressed_and_released_alone_emits_a_single_press()
    {
        var (session, commands) = NewSession();

        // Une frappe préalable sert d'ancre : sans elle, la 1re commande de la session aurait de toute façon un délai de 0.
        session.HandleKey(Key(0x41, down: true, t: 0));
        session.HandleKey(Key(0x41, down: false, t: 10));

        session.HandleKey(Key(0x11, down: true, t: 50)); // Ctrl
        session.HandleKey(Key(0x11, down: false, t: 150));

        var k = Assert.IsType<KeyboardCommand>(commands[^1]);
        Assert.Equal(0x11, k.VirtualKey);
        Assert.Equal(KeyAction.Press, k.Action);
        Assert.Equal(KeyModifiers.None, k.Modifiers);
        Assert.Equal(140, k.DelayMs); // ancré sur le relâchement (150), pas sur l'appui (50).
    }

    [Fact]
    public void Modifier_used_in_a_chord_emits_nothing_by_itself()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x11, down: true, t: 0)); // Ctrl maintenu
        session.HandleKey(Key(0x43, down: true, t: 10, character: 'c')); // C : mais Ctrl+C n'est pas éligible texte (modificateur Ctrl)
        session.HandleKey(Key(0x43, down: false, t: 20));
        session.HandleKey(Key(0x11, down: false, t: 30));

        Assert.DoesNotContain(commands, c => c is KeyboardCommand { VirtualKey: 0x11 });
        Assert.Collection(commands,
            c => Assert.Equal(KeyAction.Down, Assert.IsType<KeyboardCommand>(c).Action),
            c => Assert.Equal(KeyAction.Up, Assert.IsType<KeyboardCommand>(c).Action));
        Assert.All(commands, c => Assert.Equal(KeyModifiers.Ctrl, Assert.IsType<KeyboardCommand>(c).Modifiers));
    }

    [Fact]
    public void Holding_a_regular_key_while_another_key_is_pressed_keeps_correct_order()
    {
        // Cas emblématique (macros de jeu) : maintenir W, appuyer/relâcher Espace pendant ce temps, puis relâcher W.
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x57, down: true, t: 0));    // W down
        session.HandleKey(Key(0x20, down: true, t: 10));   // Espace down
        session.HandleKey(Key(0x20, down: false, t: 20));  // Espace up
        session.HandleKey(Key(0x57, down: false, t: 500)); // W up (bien après)

        Assert.Equal(4, commands.Count);
        Assert.Equal([0x57, 0x20, 0x20, 0x57], commands.Cast<KeyboardCommand>().Select(c => c.VirtualKey));
        Assert.Equal([KeyAction.Down, KeyAction.Down, KeyAction.Up, KeyAction.Up], commands.Cast<KeyboardCommand>().Select(c => c.Action));
        Assert.All(commands, c => Assert.True(Assert.IsType<KeyboardCommand>(c).DelayMs >= 0));
    }

    [Fact]
    public void Key_auto_repeat_is_not_recorded_twice()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x41, down: true, t: 0, character: null));
        session.HandleKey(Key(0x41, down: true, t: 30, character: null)); // répétition système
        session.HandleKey(Key(0x41, down: true, t: 60, character: null));
        session.HandleKey(Key(0x41, down: false, t: 90));

        Assert.Equal(2, commands.Count); // un seul Down, un seul Up.
    }

    [Fact]
    public void Injected_key_events_are_ignored()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x41, down: true, t: 0, injected: true));
        session.HandleKey(Key(0x41, down: false, t: 10, injected: true));

        Assert.Empty(commands);
    }

    // ------------------------------------------------------------------------------------------------ Clavier : texte

    [Fact]
    public void Typed_characters_merge_into_a_single_text_command()
    {
        var (session, commands) = NewSession();

        foreach (var (ch, t) in new[] { ('b', 0L), ('o', 20L), ('n', 40L) })
        {
            session.HandleKey(Key(0, down: true, t: t, character: ch));
            session.HandleKey(Key(0, down: false, t: t + 5));
        }

        session.Flush(100);

        var text = Assert.IsType<TextCommand>(Assert.Single(commands));
        Assert.Equal("bon", text.Text);
        Assert.Equal(0, text.DelayMs);
    }

    [Fact]
    public void Text_buffer_flushes_before_a_non_text_key()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0x41, down: false, t: 5));
        session.HandleKey(Key(0x0D, down: true, t: 10, character: '\r')); // Entrée : caractère de contrôle, pas du texte.
        session.HandleKey(Key(0x0D, down: false, t: 15));

        Assert.Equal(3, commands.Count);
        Assert.Equal("a", Assert.IsType<TextCommand>(commands[0]).Text);
        Assert.Equal(KeyAction.Down, Assert.IsType<KeyboardCommand>(commands[1]).Action);
    }

    [Fact]
    public void Text_buffer_flushes_before_a_mouse_event()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0x41, down: false, t: 5));
        session.HandleMouse(Down(MouseButton.Left, 1, 1, 10));

        Assert.Equal("a", Assert.IsType<TextCommand>(commands[0]).Text);
    }

    [Fact]
    public void Text_buffer_times_out_into_two_separate_commands()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0, down: false, t: 5));
        session.HandleKey(Key(0, down: true, t: 5000, character: 'b')); // bien après le délai de fusion.
        session.HandleKey(Key(0, down: false, t: 5005));
        session.Flush(5010);

        var texts = commands.OfType<TextCommand>().ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal("a", texts[0].Text);
        Assert.Equal("b", texts[1].Text);
    }

    [Fact]
    public void RecordTypedTextAsString_false_records_individual_key_presses_instead()
    {
        var (session, commands) = NewSession(new RecordingOptions { RecordTypedTextAsString = false });

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0x41, down: false, t: 5));

        Assert.Empty(commands.OfType<TextCommand>());
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public void Flush_emits_the_pending_text_buffer()
    {
        var (session, commands) = NewSession();

        session.HandleKey(Key(0, down: true, t: 0, character: 'x'));
        session.HandleKey(Key(0, down: false, t: 5));
        session.Flush(10);

        Assert.Equal("x", Assert.IsType<TextCommand>(Assert.Single(commands)).Text);
    }

    // ------------------------------------------------------------------------------------------------ Confidentialité

    [Fact]
    public void Sensitive_field_suppresses_all_keyboard_capture()
    {
        var sensitive = true;
        var (session, commands) = NewSession(sensitive: () => sensitive);

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0x41, down: false, t: 5));

        Assert.Empty(commands);
    }

    [Fact]
    public void Leaving_a_sensitive_field_discards_any_partial_state_and_resumes_normally()
    {
        var sensitive = true;
        var (session, commands) = NewSession(sensitive: () => sensitive);

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a')); // ignoré (champ sensible)
        sensitive = false;
        session.HandleKey(Key(0x41, down: false, t: 5)); // relâchement d'une touche jamais vue comme "down" ici
        session.HandleKey(Key(0x42, down: true, t: 10, character: 'b'));
        session.HandleKey(Key(0x42, down: false, t: 15));
        session.Flush(20);

        // Le "up" orphelin de 'a' est traité comme une touche déjà enfoncée avant l'enregistrement (Up littéral),
        // 'b' est enregistré normalement dans une commande Texte séparée.
        Assert.Contains(commands, c => c is KeyboardCommand { VirtualKey: 0x41, Action: KeyAction.Up });
        Assert.Contains(commands, c => c is TextCommand { Text: "b" });
    }

    [Fact]
    public void DontRecordPasswordFields_false_ignores_the_sensitive_guard()
    {
        var (session, commands) = NewSession(new RecordingOptions { DontRecordPasswordFields = false }, sensitive: () => true);

        session.HandleKey(Key(0x41, down: true, t: 0, character: 'a'));
        session.HandleKey(Key(0x41, down: false, t: 5));
        session.Flush(10);

        Assert.NotEmpty(commands);
    }

    // ------------------------------------------------------------------------------------------------ Délais

    [Fact]
    public void RecordDelays_false_zeroes_every_delay()
    {
        var (session, commands) = NewSession(new RecordingOptions { RecordDelays = false });

        session.HandleMouse(Down(MouseButton.Left, 0, 0, 0));
        session.HandleMouse(Up(MouseButton.Left, 0, 0, 5000));

        Assert.All(commands, c => Assert.Equal(0, c.DelayMs));
    }

    [Fact]
    public void DelayCapMs_clamps_long_pauses()
    {
        var (session, commands) = NewSession(new RecordingOptions { DelayCapMs = 200 });

        session.HandleMouse(Down(MouseButton.Left, 0, 0, 0));
        session.HandleMouse(Up(MouseButton.Left, 0, 0, 5000));

        Assert.Equal(200, commands[1].DelayMs);
    }

    [Fact]
    public void Flush_emits_a_move_that_never_got_a_following_event()
    {
        var (session, commands) = NewSession();

        session.HandleMouse(Move(1, 2, 0));
        session.Flush(50);

        var move = Assert.IsType<MouseCommand>(Assert.Single(commands));
        Assert.Equal(MouseAction.Move, move.Action);
    }
}
