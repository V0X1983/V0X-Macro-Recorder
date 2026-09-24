using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Tests;

public sealed class CommandDescriberTests
{
    [Theory]
    [InlineData(MouseAction.Move, MouseButton.Left, "Déplacer vers (10, 20)")]
    [InlineData(MouseAction.Click, MouseButton.Left, "Clic gauche à (10, 20)")]
    [InlineData(MouseAction.Click, MouseButton.Right, "Clic droit à (10, 20)")]
    [InlineData(MouseAction.DoubleClick, MouseButton.Middle, "Double-clic du milieu à (10, 20)")]
    [InlineData(MouseAction.Down, MouseButton.Left, "Bouton gauche enfoncé à (10, 20)")]
    [InlineData(MouseAction.Up, MouseButton.Right, "Bouton droit relâché à (10, 20)")]
    public void Mouse_details(MouseAction action, MouseButton button, string expected)
    {
        var command = new MouseCommand { Action = action, Button = button, X = 10, Y = 20 };

        Assert.Equal(expected, CommandDescriber.GetDetails(command));
    }

    [Fact]
    public void Mouse_coordinate_modes_are_spelled_out()
    {
        Assert.Equal(
            "Clic gauche à (5, 6) dans la fenêtre active",
            CommandDescriber.GetDetails(new MouseCommand { X = 5, Y = 6, CoordinateMode = CoordinateMode.ActiveWindow }));
        Assert.Equal(
            "Déplacer vers (+5, -6) depuis la position actuelle",
            CommandDescriber.GetDetails(new MouseCommand { Action = MouseAction.Move, X = 5, Y = -6, CoordinateMode = CoordinateMode.Relative }));
    }

    [Fact]
    public void ActiveWindow_details_show_the_captured_window_title_when_present()
    {
        var command = new MouseCommand { X = 5, Y = 6, CoordinateMode = CoordinateMode.ActiveWindow, WindowTitle = "Bloc-notes" };

        Assert.Equal("Clic gauche à (5, 6) dans « Bloc-notes »", CommandDescriber.GetDetails(command));
    }

    [Theory]
    [InlineData(1, "Molette vers le haut (1 cran)")]
    [InlineData(3, "Molette vers le haut (3 crans)")]
    [InlineData(-2, "Molette vers le bas (2 crans)")]
    public void Wheel_details(int notches, string expected)
    {
        var command = new MouseCommand { Action = MouseAction.Wheel, WheelDelta = notches };

        Assert.Equal(expected, CommandDescriber.GetDetails(command));
    }

    [Theory]
    [InlineData(KeyAction.Press, KeyModifiers.None, 0x41, "Appuyer sur A")]
    [InlineData(KeyAction.Press, KeyModifiers.Ctrl | KeyModifiers.Shift, 0x53, "Appuyer sur Ctrl+Maj+S")]
    [InlineData(KeyAction.Down, KeyModifiers.None, 0x0D, "Maintenir Entrée enfoncée")]
    [InlineData(KeyAction.Up, KeyModifiers.Alt, 0x70, "Relâcher Alt+F1")]
    [InlineData(KeyAction.Press, KeyModifiers.None, 0xFE, "Appuyer sur Touche 0xFE")]
    public void Keyboard_details(KeyAction action, KeyModifiers modifiers, int key, string expected)
    {
        var command = new KeyboardCommand { Action = action, Modifiers = modifiers, VirtualKey = key };

        Assert.Equal(expected, CommandDescriber.GetDetails(command));
    }

    [Fact]
    public void Text_details_flatten_line_breaks_and_truncate()
    {
        Assert.Equal("Saisir « a↵b »", CommandDescriber.GetDetails(new TextCommand { Text = "a\r\nb" }));
        Assert.Equal("Saisir du texte (vide)", CommandDescriber.GetDetails(new TextCommand()));

        var details = CommandDescriber.GetDetails(new TextCommand { Text = new string('x', 100) });
        Assert.Equal("Saisir « " + new string('x', 60) + "… »", details);
    }

    [Fact]
    public void Wait_details_show_fixed_and_random_durations()
    {
        Assert.Equal("Attendre 500 ms", CommandDescriber.GetDetails(new WaitCommand { DurationMs = 500 }));
        Assert.Equal("Attendre 1,5 s", CommandDescriber.GetDetails(new WaitCommand { DurationMs = 1500 }));
        Assert.Equal("Attendre de 500 ms à 1,5 s", CommandDescriber.GetDetails(new WaitCommand { DurationMs = 500, RandomExtraMs = 1000 }));
    }

    [Fact]
    public void Wait_details_describe_the_window_and_key_modes()
    {
        Assert.Equal("Attendre qu'une fenêtre apparaisse", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.WindowAppears }));
        Assert.Equal("Attendre que « Bloc-notes » apparaisse", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.WindowAppears, WindowTitle = "Bloc-notes" }));
        Assert.Equal("Attendre que « Bloc-notes » disparaisse", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.WindowDisappears, WindowTitle = "Bloc-notes" }));
        Assert.Equal("Attendre n'importe quelle touche", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.KeyPress }));
        Assert.Equal("Attendre la touche A", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.KeyPress, VirtualKey = 'A' }));
    }

    [Fact]
    public void Comment_details_show_only_the_first_line()
    {
        Assert.Equal("ligne 1…", CommandDescriber.GetDetails(new CommentCommand { Text = "ligne 1\nligne 2" }));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(250, "250 ms")]
    [InlineData(1000, "1 s")]
    [InlineData(2500, "2,5 s")]
    public void Delay_text_for_a_regular_command(int delay, string expected)
    {
        Assert.Equal(expected, CommandDescriber.GetDelayText(new KeyboardCommand { DelayMs = delay }));
    }

    [Fact]
    public void Wait_and_comment_do_not_show_a_delay()
    {
        Assert.Equal("", CommandDescriber.GetDelayText(new WaitCommand { DelayMs = 300 }));
        Assert.Equal("", CommandDescriber.GetDelayText(new CommentCommand { DelayMs = 300 }));
    }

    [Fact]
    public void Titles_are_in_french()
    {
        Assert.Equal("Souris", CommandDescriber.GetTitle(new MouseCommand()));
        Assert.Equal("Clavier", CommandDescriber.GetTitle(new KeyboardCommand()));
        Assert.Equal("Texte", CommandDescriber.GetTitle(new TextCommand()));
        Assert.Equal("Attente", CommandDescriber.GetTitle(new WaitCommand()));
        Assert.Equal("Commentaire", CommandDescriber.GetTitle(new CommentCommand()));
        Assert.Equal("Presse-papiers", CommandDescriber.GetTitle(new ClipboardCommand()));
        Assert.Equal("Programme", CommandDescriber.GetTitle(new LaunchCommand()));
        Assert.Equal("Adresse web", CommandDescriber.GetTitle(new OpenUrlCommand()));
        Assert.Equal("Fenêtre", CommandDescriber.GetTitle(new WindowCommand()));
        Assert.Equal("Pixel", CommandDescriber.GetTitle(new PixelCommand()));
        Assert.Equal("Son", CommandDescriber.GetTitle(new SoundCommand()));
        Assert.Equal("Message", CommandDescriber.GetTitle(new MessageCommand()));
        Assert.Equal("Image", CommandDescriber.GetTitle(new ImageSearchCommand()));
        Assert.Equal("Boucle", CommandDescriber.GetTitle(new LoopCommand()));
        Assert.Equal("Fin boucle", CommandDescriber.GetTitle(new EndLoopCommand()));
        Assert.Equal("Variable", CommandDescriber.GetTitle(new VariableCommand()));
        Assert.Equal("Étiquette", CommandDescriber.GetTitle(new LabelCommand()));
        Assert.Equal("Aller à", CommandDescriber.GetTitle(new GotoCommand()));
        Assert.Equal("Arrêter", CommandDescriber.GetTitle(new StopCommand()));
        Assert.Equal("Appeler une macro", CommandDescriber.GetTitle(new CallCommand()));
        Assert.Equal("Pause", CommandDescriber.GetTitle(new PauseCommand()));
        Assert.Equal("Script C#", CommandDescriber.GetTitle(new ScriptCommand()));
        Assert.Equal("Saisie protégée", CommandDescriber.GetTitle(new SecureInputCommand()));
    }

    [Fact]
    public void Secure_input_details_never_reveal_the_secret()
    {
        Assert.Equal("Demander « Mot de passe » à la lecture", CommandDescriber.GetDetails(new SecureInputCommand { PromptAtPlayback = true, PromptLabel = "Mot de passe" }));
        Assert.Equal("Secret chiffré enregistré (ne jamais afficher)", CommandDescriber.GetDetails(new SecureInputCommand { PromptAtPlayback = false, ProtectedValueBase64 = "should-never-appear-in-output" }));
    }

    [Fact]
    public void Label_goto_call_and_pause_details()
    {
        Assert.Equal("« début »", CommandDescriber.GetDetails(new LabelCommand { Name = "début" }));
        Assert.Equal("Aller à « début »", CommandDescriber.GetDetails(new GotoCommand { TargetLabel = "début" }));
        Assert.Equal("Appeler « sub.v0xmacro »", CommandDescriber.GetDetails(new CallCommand { MacroFilePath = "sub.v0xmacro" }));
        Assert.Equal("Attendre n'importe quelle touche", CommandDescriber.GetDetails(new PauseCommand()));
    }

    [Fact]
    public void Script_details_show_first_line_or_empty_placeholder()
    {
        Assert.Equal("Script vide", CommandDescriber.GetDetails(new ScriptCommand()));
        Assert.Equal("Mouse.Click();…", CommandDescriber.GetDetails(new ScriptCommand { Code = "Mouse.Click();\nWindow.Close(\"Bloc-notes\");" }));
    }

    [Fact]
    public void Label_does_not_show_a_delay()
    {
        Assert.Equal("", CommandDescriber.GetDelayText(new LabelCommand { DelayMs = 300 }));
    }

    [Fact]
    public void Loop_and_variable_details()
    {
        Assert.Equal("Répéter 5 fois", CommandDescriber.GetDetails(new LoopCommand { Mode = LoopMode.RepeatCount, RepeatCount = 5 }));
        Assert.Equal("Pour chaque ligne de « f.txt »", CommandDescriber.GetDetails(new LoopCommand { Mode = LoopMode.ForEachLine, FilePath = "f.txt" }));
        Assert.Equal("Définir {var:x} = « 5 »", CommandDescriber.GetDetails(new VariableCommand { Mode = VariableMode.Set, Name = "x", Value = "5" }));
        Assert.Equal("Incrémenter {var:x} de 1", CommandDescriber.GetDetails(new VariableCommand { Mode = VariableMode.Increment, Name = "x", Value = "" }));
    }

    [Fact]
    public void Loop_and_endloop_do_not_show_a_delay()
    {
        Assert.Equal("", CommandDescriber.GetDelayText(new LoopCommand { DelayMs = 300 }));
        Assert.Equal("", CommandDescriber.GetDelayText(new EndLoopCommand { DelayMs = 300 }));
    }

    [Fact]
    public void Image_search_details()
    {
        Assert.Equal(
            "Chercher l'image (16×16, tolérance 10%)",
            CommandDescriber.GetDetails(new ImageSearchCommand { TemplateWidth = 16, TemplateHeight = 16, TolerancePercent = 10 }));
        Assert.Equal(
            "Chercher l'image (16×16, tolérance 10%) puis cliquer",
            CommandDescriber.GetDetails(new ImageSearchCommand { TemplateWidth = 16, TemplateHeight = 16, TolerancePercent = 10, ClickIfFound = true }));
        Assert.Equal(
            "Chercher l'image (16×16, tolérance 10%) puis double-cliquer",
            CommandDescriber.GetDetails(new ImageSearchCommand { TemplateWidth = 16, TemplateHeight = 16, TolerancePercent = 10, ClickIfFound = true, DoubleClick = true }));
    }

    [Fact]
    public void Sound_and_message_details()
    {
        Assert.Equal("Jouer « ding.wav »", CommandDescriber.GetDetails(new SoundCommand { FilePath = "ding.wav" }));
        Assert.Equal("« Titre » : salut", CommandDescriber.GetDetails(new MessageCommand { Title = "Titre", Text = "salut" }));
    }

    [Fact]
    public void Pixel_details()
    {
        Assert.Equal("Tester si (5, 6) vaut #FF0000", CommandDescriber.GetDetails(new PixelCommand { Mode = PixelActionMode.Test, X = 5, Y = 6, ExpectedColorHex = "#FF0000" }));
        Assert.Equal("Attendre que (5, 6) devienne #FF0000", CommandDescriber.GetDetails(new PixelCommand { Mode = PixelActionMode.Wait, X = 5, Y = 6, ExpectedColorHex = "#FF0000" }));
        Assert.Equal("Attendre que (1, 2) devienne #010203", CommandDescriber.GetDetails(new WaitCommand { Mode = WaitMode.PixelMatch, PixelX = 1, PixelY = 2, PixelColorHex = "#010203" }));
    }

    [Fact]
    public void Window_details()
    {
        Assert.Equal("Activer « Bloc-notes »", CommandDescriber.GetDetails(new WindowCommand { Action = WindowAction.Activate, WindowTitle = "Bloc-notes" }));
        Assert.Equal("Fermer « Bloc-notes »", CommandDescriber.GetDetails(new WindowCommand { Action = WindowAction.Close, WindowTitle = "Bloc-notes" }));
        Assert.Equal("Déplacer « Bloc-notes » à (10, 20), 300×400", CommandDescriber.GetDetails(new WindowCommand { Action = WindowAction.MoveResize, WindowTitle = "Bloc-notes", X = 10, Y = 20, Width = 300, Height = 400 }));
    }

    [Fact]
    public void Launch_and_url_details()
    {
        Assert.Equal("Lancer « notepad.exe »", CommandDescriber.GetDetails(new LaunchCommand { Path = "notepad.exe" }));
        Assert.Equal("Lancer « notepad.exe » (attendre la fin)", CommandDescriber.GetDetails(new LaunchCommand { Path = "notepad.exe", WaitForExit = true }));
        Assert.Equal("Commande shell « dir »", CommandDescriber.GetDetails(new LaunchCommand { Mode = LaunchMode.ShellCommand, Path = "dir" }));
        Assert.Equal("Ouvrir « https://example.test »", CommandDescriber.GetDetails(new OpenUrlCommand { Path = "https://example.test" }));
    }

    [Fact]
    public void Clipboard_details_describe_each_action()
    {
        Assert.Equal("Copier « salut »", CommandDescriber.GetDetails(new ClipboardCommand { Action = ClipboardAction.Copy, Text = "salut" }));
        Assert.Equal("Coller (Ctrl+V)", CommandDescriber.GetDetails(new ClipboardCommand { Action = ClipboardAction.Paste }));
        Assert.Equal("Lire dans {var:x}", CommandDescriber.GetDetails(new ClipboardCommand { Action = ClipboardAction.ReadToVariable, VariableName = "x" }));
    }

    [Fact]
    public void Recent_files_are_deduplicated_ordered_and_capped()
    {
        var settings = new AppSettings();

        for (var i = 0; i < AppSettings.MaxRecentFiles + 3; i++)
        {
            settings.AddRecentFile($@"C:\macros\m{i}.v0xmacro");
        }

        settings.AddRecentFile(@"C:\MACROS\M5.V0XMACRO");

        Assert.Equal(AppSettings.MaxRecentFiles, settings.RecentFiles.Count);
        Assert.Equal(@"C:\MACROS\M5.V0XMACRO", settings.RecentFiles[0]);
        Assert.Single(settings.RecentFiles, p => p.EndsWith("m5.v0xmacro", StringComparison.OrdinalIgnoreCase));

        settings.RemoveRecentFile(@"c:\macros\m12.v0xmacro");
        Assert.DoesNotContain(settings.RecentFiles, p => p.EndsWith("m12.v0xmacro", StringComparison.OrdinalIgnoreCase));
    }
}
