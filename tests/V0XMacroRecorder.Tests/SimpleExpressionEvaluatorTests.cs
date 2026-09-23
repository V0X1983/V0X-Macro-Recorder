using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Tests;

public sealed class SimpleExpressionEvaluatorTests
{
    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("10 - 4", 6)]
    [InlineData("3 * 4", 12)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("2 + 3 * 4", 14)] // priorité de * sur +.
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("-5 + 2", -3)]
    [InlineData("1.5 + 2.5", 4)]
    public void Evaluates_basic_arithmetic(string expression, double expected)
    {
        Assert.Equal(expected, SimpleExpressionEvaluator.Evaluate(expression), 3);
    }

    [Fact]
    public void Division_by_zero_returns_zero()
    {
        Assert.Equal(0, SimpleExpressionEvaluator.Evaluate("5 / 0"));
    }

    [Fact]
    public void Malformed_expression_returns_zero()
    {
        Assert.Equal(0, SimpleExpressionEvaluator.Evaluate("2 + "));
        Assert.Equal(0, SimpleExpressionEvaluator.Evaluate("abc"));
        Assert.Equal(0, SimpleExpressionEvaluator.Evaluate(""));
    }

    [Fact]
    public void Nested_parentheses_work()
    {
        Assert.Equal(12, SimpleExpressionEvaluator.Evaluate("2 * (3 + (4 - 1))"));
    }
}
