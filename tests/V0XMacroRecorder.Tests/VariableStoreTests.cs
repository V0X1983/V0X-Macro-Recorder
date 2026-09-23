using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Tests;

public sealed class VariableStoreTests
{
    [Fact]
    public void Undefined_variable_reads_as_empty_string()
    {
        var vars = new VariableStore();

        Assert.Equal("", vars.Get("x"));
    }

    [Fact]
    public void Set_then_get_round_trips()
    {
        var vars = new VariableStore();

        vars.Set("x", "hello");

        Assert.Equal("hello", vars.Get("x"));
    }

    [Fact]
    public void Names_are_case_insensitive()
    {
        var vars = new VariableStore();

        vars.Set("Counter", "1");

        Assert.Equal("1", vars.Get("COUNTER"));
        Assert.Equal("1", vars.Get("counter"));
    }

    [Fact]
    public void Clear_removes_every_variable()
    {
        var vars = new VariableStore();
        vars.Set("x", "1");
        vars.Set("y", "2");

        vars.Clear();

        Assert.Equal("", vars.Get("x"));
        Assert.Equal("", vars.Get("y"));
    }

    [Fact]
    public void TryGetNumber_succeeds_for_a_numeric_value()
    {
        var vars = new VariableStore();
        vars.Set("x", "3.5");

        Assert.True(vars.TryGetNumber("x", out var value));
        Assert.Equal(3.5, value);
    }

    [Fact]
    public void TryGetNumber_fails_for_a_non_numeric_or_undefined_value()
    {
        var vars = new VariableStore();
        vars.Set("x", "abc");

        Assert.False(vars.TryGetNumber("x", out _));
        Assert.False(vars.TryGetNumber("undefined", out _));
    }
}
