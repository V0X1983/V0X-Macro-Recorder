using System.Text.Json.Nodes;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests;

public sealed class MacroSerializerTests
{
    private static Macro SampleMacro() => new()
    {
        Name = "Démo",
        PlaybackSpeed = 2.5,
        RepeatCount = 3,
        Commands =
        [
            new MouseCommand
            {
                Action = MouseAction.DoubleClick, Button = MouseButton.Right, X = 120, Y = -40,
                CoordinateMode = CoordinateMode.ActiveWindow, DelayMs = 250,
                WindowTitle = "Bloc-notes", WindowClassName = "Notepad",
            },
            new MouseCommand { Action = MouseAction.Wheel, WheelDelta = -3 },
            new KeyboardCommand
            {
                VirtualKey = 0x53, Action = KeyAction.Press, Modifiers = KeyModifiers.Ctrl | KeyModifiers.Shift, DelayMs = 10,
                ScanCode = 0x1F, IsExtendedKey = true,
            },
            new TextCommand { Text = "Bonjour\nle monde « é »", CharacterDelayMs = 15 },
            new WaitCommand { DurationMs = 500, RandomExtraMs = 250 },
            new CommentCommand { Text = "Fin de la démo" },
        ],
    };

    [Fact]
    public void Round_trip_preserves_every_command_type_and_field()
    {
        var original = SampleMacro();

        var restored = MacroSerializer.Deserialize(MacroSerializer.Serialize(original));

        Assert.Equal("Démo", restored.Name);
        Assert.Equal(2.5, restored.PlaybackSpeed);
        Assert.Equal(3, restored.RepeatCount);
        Assert.Equal(6, restored.Commands.Count);

        var mouse = Assert.IsType<MouseCommand>(restored.Commands[0]);
        Assert.Equal(MouseAction.DoubleClick, mouse.Action);
        Assert.Equal(MouseButton.Right, mouse.Button);
        Assert.Equal(120, mouse.X);
        Assert.Equal(-40, mouse.Y);
        Assert.Equal(CoordinateMode.ActiveWindow, mouse.CoordinateMode);
        Assert.Equal(250, mouse.DelayMs);
        Assert.Equal("Bloc-notes", mouse.WindowTitle);
        Assert.Equal("Notepad", mouse.WindowClassName);

        Assert.Equal(-3, Assert.IsType<MouseCommand>(restored.Commands[1]).WheelDelta);

        var key = Assert.IsType<KeyboardCommand>(restored.Commands[2]);
        Assert.Equal(0x53, key.VirtualKey);
        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Shift, key.Modifiers);
        Assert.Equal(0x1F, key.ScanCode);
        Assert.True(key.IsExtendedKey);

        var text = Assert.IsType<TextCommand>(restored.Commands[3]);
        Assert.Equal("Bonjour\nle monde « é »", text.Text);
        Assert.Equal(15, text.CharacterDelayMs);

        var wait = Assert.IsType<WaitCommand>(restored.Commands[4]);
        Assert.Equal(500, wait.DurationMs);
        Assert.Equal(250, wait.RandomExtraMs);

        Assert.Equal("Fin de la démo", Assert.IsType<CommentCommand>(restored.Commands[5]).Text);
    }

    [Fact]
    public void Serialized_file_declares_the_schema_version_and_type_discriminators()
    {
        var json = JsonNode.Parse(MacroSerializer.Serialize(SampleMacro()))!.AsObject();

        Assert.Equal(MacroSerializer.CurrentSchemaVersion, (int)json["schemaVersion"]!);
        Assert.Equal("mouse", (string)json["commands"]![0]!["type"]!);
        Assert.Equal("keyboard", (string)json["commands"]![2]!["type"]!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ pas du json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"name\": \"sans version\" }")]
    [InlineData("{ \"schemaVersion\": 0 }")]
    public void Invalid_documents_raise_a_user_friendly_MacroFormatException(string json)
    {
        Assert.Throws<MacroFormatException>(() => MacroSerializer.Deserialize(json));
    }

    [Fact]
    public void Unknown_command_type_is_rejected()
    {
        var json = $$"""{ "schemaVersion": {{MacroSerializer.CurrentSchemaVersion}}, "commands": [ { "type": "teleport" } ] }""";

        Assert.Throws<MacroFormatException>(() => MacroSerializer.Deserialize(json));
    }

    [Fact]
    public void File_from_a_newer_version_is_refused_with_an_explicit_message()
    {
        var json = $$"""{ "schemaVersion": {{MacroSerializer.CurrentSchemaVersion + 1}}, "commands": [] }""";

        var ex = Assert.Throws<MacroFormatException>(() => MacroSerializer.Deserialize(json));

        Assert.Contains("plus récente", ex.Message);
    }

    [Fact]
    public void Older_files_are_migrated_step_by_step()
    {
        // Simule un format 1 où "delay" (en secondes) deviendra "delayMs" en version 2, puis un renommage en version 3.
        var migrations = new Dictionary<int, Action<JsonObject>>
        {
            [1] = root =>
            {
                foreach (var command in root["commands"]!.AsArray())
                {
                    var seconds = (double)command!["delay"]!;
                    command.AsObject().Remove("delay");
                    command["delayMs"] = (int)(seconds * 1000);
                }
            },
            [2] = root => root["name"] = ((string)root["name"]!) + " (migrée)",
        };
        const string v1 = """
            { "schemaVersion": 1, "name": "Ancienne", "commands": [ { "type": "comment", "text": "salut", "delay": 1.5 } ] }
            """;

        var macro = MacroSerializer.Deserialize(v1, currentVersion: 3, migrations);

        Assert.Equal("Ancienne (migrée)", macro.Name);
        Assert.Equal(1500, macro.Commands[0].DelayMs);
    }

    [Fact]
    public void Missing_migration_step_is_reported()
    {
        const string v1 = """{ "schemaVersion": 1, "commands": [] }""";

        Assert.Throws<MacroFormatException>(() =>
            MacroSerializer.Deserialize(v1, currentVersion: 2, new Dictionary<int, Action<JsonObject>>()));
    }

    [Fact]
    public void Out_of_range_values_are_clamped_when_loading()
    {
        var json = $$"""
            { "schemaVersion": {{MacroSerializer.CurrentSchemaVersion}}, "playbackSpeed": 500, "repeatCount": -4,
              "commands": [ { "type": "keyboard", "virtualKey": 9999, "delayMs": -10 } ] }
            """;

        var macro = MacroSerializer.Deserialize(json);

        Assert.Equal(10.0, macro.PlaybackSpeed);
        Assert.Equal(1, macro.RepeatCount);
        var key = Assert.IsType<KeyboardCommand>(macro.Commands[0]);
        Assert.Equal(254, key.VirtualKey);
        Assert.Equal(0, key.DelayMs);
    }

    [Fact]
    public void Null_commands_list_becomes_empty()
    {
        var json = $$"""{ "schemaVersion": {{MacroSerializer.CurrentSchemaVersion}}, "commands": null }""";

        Assert.Empty(MacroSerializer.Deserialize(json).Commands);
    }

    [Fact]
    public void Commands_round_trip_for_the_clipboard()
    {
        var commands = SampleMacro().Commands;

        var restored = MacroSerializer.DeserializeCommands(MacroSerializer.SerializeCommands(commands));

        Assert.Equal(commands.Count, restored.Count);
        Assert.IsType<WaitCommand>(restored[4]);
    }

    [Fact]
    public void Invalid_clipboard_content_is_rejected()
    {
        Assert.Throws<MacroFormatException>(() => MacroSerializer.DeserializeCommands("du texte quelconque"));
    }

    [Fact]
    public void Secure_input_command_and_recorded_screen_size_round_trip()
    {
        var macro = new Macro
        {
            RecordedScreenWidth = 3840,
            RecordedScreenHeight = 1080,
            Commands = [new SecureInputCommand { PromptAtPlayback = false, ProtectedValueBase64 = "opaque-blob", PromptLabel = "Mot de passe", CharacterDelayMs = 15 }],
        };

        var restored = MacroSerializer.Deserialize(MacroSerializer.Serialize(macro));

        Assert.Equal(3840, restored.RecordedScreenWidth);
        Assert.Equal(1080, restored.RecordedScreenHeight);
        var secure = Assert.IsType<SecureInputCommand>(Assert.Single(restored.Commands));
        Assert.False(secure.PromptAtPlayback);
        Assert.Equal("opaque-blob", secure.ProtectedValueBase64);
        Assert.Equal(15, secure.CharacterDelayMs);
    }

    [Fact]
    public void Macro_without_a_recorded_screen_size_defaults_to_zero()
    {
        var restored = MacroSerializer.Deserialize(MacroSerializer.Serialize(new Macro()));

        Assert.Equal(0, restored.RecordedScreenWidth);
        Assert.Equal(0, restored.RecordedScreenHeight);
    }
}
