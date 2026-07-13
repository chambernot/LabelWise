namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resultado da normalização de categoria.
/// </summary>
public class CategoryNormalizationResult
{
    public bool IsNormalized { get; set; }
    public string? NormalizedCategoryCode { get; set; }
    public string? NormalizedCategoryName { get; set; }
    public string? RawInput { get; set; }
    public string? MatchedAlias { get; set; }
    public double Confidence { get; set; }
    public string MatchType { get; set; } = "none";
    public string[] Evidence { get; set; } = [];
    public string[] CandidateCategories { get; set; } = [];
    public bool IsAmbiguous { get; set; }

    public static CategoryNormalizationResult Unknown(string rawInput)
    {
        return new CategoryNormalizationResult
        {
            IsNormalized = false,
            RawInput = rawInput,
            Confidence = 0.0,
            MatchType = "not_found"
        };
    }
}
