using V0XMacroRecorder.Core.CommandLine;

namespace V0XMacroRecorder.Tests;

public sealed class CommandLineArgsTests
{
    [Fact]
    public void No_play_option_returns_null()
    {
        Assert.Null(CommandLineArgs.ParsePlay(["C:\\macro.v0xmacro"]));
        Assert.Null(CommandLineArgs.ParsePlay([]));
    }

    [Fact]
    public void Play_without_a_path_is_ignored()
    {
        Assert.Null(CommandLineArgs.ParsePlay(["--play"]));
    }

    [Fact]
    public void Play_with_path_only()
    {
        var request = CommandLineArgs.ParsePlay(["--play", "C:\\macro.v0xmacro"]);

        Assert.NotNull(request);
        Assert.Equal("C:\\macro.v0xmacro", request!.MacroFilePath);
        Assert.False(request.Silent);
        Assert.Null(request.RepeatOverride);
    }

    [Fact]
    public void Play_is_case_insensitive_and_order_independent()
    {
        var request = CommandLineArgs.ParsePlay(["--REPEAT", "3", "--SILENT", "--PLAY", "C:\\macro.v0xmacro"]);

        Assert.NotNull(request);
        Assert.Equal("C:\\macro.v0xmacro", request!.MacroFilePath);
        Assert.True(request.Silent);
        Assert.Equal(3, request.RepeatOverride);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Invalid_repeat_values_are_ignored(string repeatText)
    {
        var request = CommandLineArgs.ParsePlay(["--play", "C:\\macro.v0xmacro", "--repeat", repeatText]);

        Assert.NotNull(request);
        Assert.Null(request!.RepeatOverride);
    }

    [Fact]
    public void Silent_flag_is_detected_regardless_of_position()
    {
        var request = CommandLineArgs.ParsePlay(["--silent", "--play", "C:\\macro.v0xmacro"]);

        Assert.NotNull(request);
        Assert.True(request!.Silent);
    }
}
