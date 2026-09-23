using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Tests.Fakes;

namespace V0XMacroRecorder.Tests;

public sealed class ConditionEvaluatorTests
{
    private static ConditionEvaluator NewEvaluator(
        out FakeWindowFinder windows, out FakePixelReader pixels, out FakeImageSearcher images, out VariableStore vars)
    {
        windows = new FakeWindowFinder();
        pixels = new FakePixelReader();
        images = new FakeImageSearcher();
        vars = new VariableStore();
        return new ConditionEvaluator(windows, pixels, images, vars);
    }

    [Fact]
    public void WindowExists_true_when_found()
    {
        var evaluator = NewEvaluator(out var windows, out _, out _, out _);
        windows.Windows[("App", null)] = 1;

        Assert.True(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.WindowExists, WindowTitle = "App" }));
    }

    [Fact]
    public void WindowExists_false_when_not_found()
    {
        var evaluator = NewEvaluator(out _, out _, out _, out _);

        Assert.False(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.WindowExists, WindowTitle = "Introuvable" }));
    }

    [Fact]
    public void PixelMatches_respects_tolerance()
    {
        var evaluator = NewEvaluator(out _, out var pixels, out _, out _);
        pixels.Colors[(1, 2)] = (250, 0, 0);

        Assert.True(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.PixelMatches, PixelX = 1, PixelY = 2, PixelColorHex = "#FF0000", PixelTolerancePercent = 10 }));
        Assert.False(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.PixelMatches, PixelX = 1, PixelY = 2, PixelColorHex = "#FF0000", PixelTolerancePercent = 0 }));
    }

    [Fact]
    public void ImageFound_true_when_the_searcher_finds_something()
    {
        var evaluator = NewEvaluator(out _, out _, out var images, out _);
        images.Result = (5, 5);

        Assert.True(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.ImageFound, ImageTemplatePngBase64 = "AAAA" }));
    }

    [Fact]
    public void ImageFound_false_without_a_template()
    {
        var evaluator = NewEvaluator(out _, out _, out var images, out _);
        images.Result = (5, 5);

        Assert.False(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.ImageFound, ImageTemplatePngBase64 = null }));
    }

    [Theory]
    [InlineData(ComparisonOperator.Equals, "abc", "abc", true)]
    [InlineData(ComparisonOperator.Equals, "abc", "xyz", false)]
    [InlineData(ComparisonOperator.NotEquals, "abc", "xyz", true)]
    [InlineData(ComparisonOperator.Contains, "hello world", "world", true)]
    public void VariableCompare_string_operators(ComparisonOperator op, string actual, string expected, bool result)
    {
        var evaluator = NewEvaluator(out _, out _, out _, out var vars);
        vars.Set("x", actual);

        Assert.Equal(result, evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.VariableCompare, VariableName = "x", ComparisonOperator = op, ComparisonValue = expected }));
    }

    [Fact]
    public void VariableCompare_numeric_greater_and_less_than()
    {
        var evaluator = NewEvaluator(out _, out _, out _, out var vars);
        vars.Set("n", "10");

        Assert.True(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.VariableCompare, VariableName = "n", ComparisonOperator = ComparisonOperator.GreaterThan, ComparisonValue = "5" }));
        Assert.False(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.VariableCompare, VariableName = "n", ComparisonOperator = ComparisonOperator.LessThan, ComparisonValue = "5" }));
    }

    [Fact]
    public void FileExists_reflects_the_real_filesystem()
    {
        var evaluator = NewEvaluator(out _, out _, out _, out _);
        var tempFile = Path.GetTempFileName();
        try
        {
            Assert.True(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.FileExists, FilePath = tempFile }));
            Assert.False(evaluator.Evaluate(new ConditionSpec { Kind = ConditionKind.FileExists, FilePath = tempFile + ".doesnotexist" }));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
