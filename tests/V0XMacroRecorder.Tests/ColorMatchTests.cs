using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Tests;

public sealed class ColorMatchTests
{
    [Fact]
    public void Exact_match_is_within_tolerance_zero()
    {
        Assert.True(ColorMatch.Matches((10, 20, 30), (10, 20, 30), 0));
    }

    [Fact]
    public void Different_color_fails_tolerance_zero()
    {
        Assert.False(ColorMatch.Matches((10, 20, 30), (11, 20, 30), 0));
    }

    [Fact]
    public void Within_tolerance_percent_matches()
    {
        // 10% de 255 = 25,5 : un écart de 20 doit passer.
        Assert.True(ColorMatch.Matches((100, 100, 100), (120, 100, 100), 10));
    }

    [Fact]
    public void Outside_tolerance_percent_fails()
    {
        Assert.False(ColorMatch.Matches((100, 100, 100), (140, 100, 100), 10));
    }

    [Fact]
    public void Tolerance_100_percent_matches_anything()
    {
        Assert.True(ColorMatch.Matches((0, 0, 0), (255, 255, 255), 100));
    }

    [Theory]
    [InlineData("#FF0080", 255, 0, 128)]
    [InlineData("ff0080", 255, 0, 128)]
    [InlineData("#000000", 0, 0, 0)]
    public void ParseHex_reads_rrggbb(string hex, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), ColorMatch.ParseHex(hex));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#ZZZZZZ")]
    [InlineData("#FFF")]
    public void ParseHex_invalid_input_returns_black(string? hex)
    {
        Assert.Equal((0, 0, 0), ColorMatch.ParseHex(hex));
    }

    [Fact]
    public void ToHex_round_trips()
    {
        Assert.Equal("#FF0080", ColorMatch.ToHex((255, 0, 128)));
    }
}
