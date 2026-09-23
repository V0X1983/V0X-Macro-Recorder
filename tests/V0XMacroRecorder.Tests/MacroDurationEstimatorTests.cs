using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests;

public sealed class MacroDurationEstimatorTests
{
    [Fact]
    public void Empty_macro_is_zero()
    {
        Assert.Equal(0, MacroDurationEstimator.EstimateMilliseconds([]));
    }

    [Fact]
    public void Sums_the_delay_of_every_command()
    {
        var commands = new List<MacroCommand>
        {
            new CommentCommand { DelayMs = 100 },
            new CommentCommand { DelayMs = 250 },
        };

        Assert.Equal(350, MacroDurationEstimator.EstimateMilliseconds(commands));
    }

    [Fact]
    public void Fixed_delay_wait_adds_its_duration_and_half_the_random_extra()
    {
        var commands = new List<MacroCommand>
        {
            new WaitCommand { Mode = WaitMode.FixedDelay, DurationMs = 1000, RandomExtraMs = 500, DelayMs = 0 },
        };

        Assert.Equal(1250, MacroDurationEstimator.EstimateMilliseconds(commands));
    }

    [Fact]
    public void Event_waits_only_count_their_own_delay_not_the_unknown_wait_itself()
    {
        var commands = new List<MacroCommand>
        {
            new WaitCommand { Mode = WaitMode.WindowAppears, TimeoutMs = 60000, DelayMs = 50 },
        };

        Assert.Equal(50, MacroDurationEstimator.EstimateMilliseconds(commands));
    }

    [Fact]
    public void Text_command_adds_character_count_times_character_delay()
    {
        var commands = new List<MacroCommand>
        {
            new TextCommand { Text = "Bonjour", CharacterDelayMs = 20, DelayMs = 10 },
        };

        Assert.Equal(10 + 7 * 20, MacroDurationEstimator.EstimateMilliseconds(commands));
    }
}
