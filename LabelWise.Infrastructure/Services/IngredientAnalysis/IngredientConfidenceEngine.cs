using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class IngredientConfidenceEngine
{
    public string EvaluateOverall(
        bool openAiVisionUsed,
        bool ocrProviderUsed,
        bool documentIntelligenceUsed,
        int ingredientCount,
        int claimCount,
        int sourceConflictCount,
        string imageQualityConfidence)
    {
        var score = 0;
        if (openAiVisionUsed) score += 2;
        if (ocrProviderUsed) score += 1;
        if (documentIntelligenceUsed) score += 1;
        if (ingredientCount > 0) score += 2;
        if (claimCount > 0) score += 1;
        if (imageQualityConfidence == "high") score += 1;
        score -= Math.Min(2, sourceConflictCount);

        var confidence = score >= 6 ? "high" : score >= 3 ? "medium" : "low";
        return sourceConflictCount > 0 && confidence == "high" ? "medium" : confidence;
    }

    public int CountConflicts(IReadOnlyList<string> claims, IReadOnlyList<AllergenRiskDto> allergenRisks)
    {
        var conflicts = 0;
        if (claims.Any(claim => IngredientTextNormalizer.ContainsAny(claim, ["pode conter", "traços de", "tracos de", "fabricado em equipamento", "fabricado em linha"])))
            conflicts++;

        foreach (var risk in allergenRisks)
        {
            if (risk.RiskType == "contains" && HasAbsenceClaim(claims, risk.Name))
                conflicts++;

            if (risk.RiskType is "may_contain" or "cross_contamination")
                conflicts++;
        }

        return conflicts;
    }

    public string EvaluateImageQuality(bool preparedSuccessfully, int rawTextLength)
    {
        if (preparedSuccessfully && rawTextLength >= 300)
            return "high";

        if (preparedSuccessfully || rawTextLength >= 120)
            return "medium";

        return "low";
    }

    public string EvaluateSemanticConfidence(
        string ocrConfidence,
        string classificationConfidence,
        string completenessStatus,
        int ingredientCount,
        int claimCount,
        int allergenRiskCount,
        int sourceConflictCount)
    {
        var score = 0;
        score += ocrConfidence switch { "high" => 30, "medium" => 18, _ => 6 };
        score += classificationConfidence switch { "high" => 25, "medium" => 15, _ => 5 };
        score += Math.Min(20, ingredientCount * 4);
        score += Math.Min(15, (claimCount + allergenRiskCount) * 5);
        score -= Math.Min(25, sourceConflictCount * 8);

        if (completenessStatus == "partial")
            score -= 20;
        else if (completenessStatus == "insufficient")
            score -= 40;

        var confidence = score >= 70 ? "high" : score >= 40 ? "medium" : "low";
        if (confidence == "low" && completenessStatus == "partial" && (claimCount > 0 || allergenRiskCount > 0))
            return "medium";

        return confidence;
    }

    private static bool HasAbsenceClaim(IReadOnlyList<string> claims, string allergen)
    {
        var normalizedAllergen = IngredientTextNormalizer.Normalize(allergen);
        return claims
            .Select(IngredientTextNormalizer.Normalize)
            .Any(claim =>
                (claim.Contains("nao contem", StringComparison.Ordinal) || claim.Contains("sem ", StringComparison.Ordinal)) &&
                claim.Contains(normalizedAllergen, StringComparison.Ordinal));
    }
}
