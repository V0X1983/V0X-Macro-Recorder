using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>Évalue une <see cref="ConditionSpec"/> (commande Si, boucle Tant que), à partir des services déjà utilisés ailleurs dans <see cref="MacroPlayer"/>.</summary>
public sealed class ConditionEvaluator(IWindowFinder windowFinder, IPixelReader pixelReader, IImageSearcher imageSearcher, VariableStore variables)
{
    public bool Evaluate(ConditionSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind switch
        {
            ConditionKind.WindowExists => windowFinder.FindWindow(spec.WindowTitle, spec.WindowClassName) is not null,
            ConditionKind.PixelMatches => pixelReader.GetPixelColor(spec.PixelX, spec.PixelY) is { } c
                && ColorMatch.Matches(c, ColorMatch.ParseHex(spec.PixelColorHex), spec.PixelTolerancePercent),
            ConditionKind.ImageFound => !string.IsNullOrEmpty(spec.ImageTemplatePngBase64)
                && imageSearcher.Find(Convert.FromBase64String(spec.ImageTemplatePngBase64), spec.SearchRegion, spec.ImageTolerancePercent) is not null,
            ConditionKind.VariableCompare => EvaluateVariableCompare(spec),
            ConditionKind.FileExists => !string.IsNullOrEmpty(spec.FilePath) && File.Exists(spec.FilePath),
            _ => false,
        };
    }

    private bool EvaluateVariableCompare(ConditionSpec spec)
    {
        if (string.IsNullOrEmpty(spec.VariableName))
        {
            return false;
        }

        var actual = variables.Get(spec.VariableName);
        var expected = spec.ComparisonValue;

        if (spec.ComparisonOperator == ComparisonOperator.Contains)
        {
            return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        if (spec.ComparisonOperator is ComparisonOperator.GreaterThan or ComparisonOperator.LessThan)
        {
            return variables.TryGetNumber(spec.VariableName, out var actualNumber)
                && double.TryParse(expected, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expectedNumber)
                && (spec.ComparisonOperator == ComparisonOperator.GreaterThan ? actualNumber > expectedNumber : actualNumber < expectedNumber);
        }

        var equal = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        return spec.ComparisonOperator == ComparisonOperator.NotEquals ? !equal : equal;
    }
}
