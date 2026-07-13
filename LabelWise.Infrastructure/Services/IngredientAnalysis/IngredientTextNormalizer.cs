using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

internal static partial class IngredientTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return WhitespaceRegex().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    public static bool ContainsAny(string source, IEnumerable<string> terms) =>
        terms.Any(term => Normalize(source).Contains(Normalize(term), StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
