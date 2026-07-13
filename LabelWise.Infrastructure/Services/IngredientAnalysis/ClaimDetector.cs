using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class ClaimDetector
{
    public List<ClaimDetectionDto> Detect(IReadOnlyList<string> claims)
    {
        return claims
            .Select(Create)
            .GroupBy(claim => IngredientTextNormalizer.Normalize(claim.Text))
            .Select(group => group.First())
            .OrderBy(claim => claim.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ClaimDetectionDto Create(string claim)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        return new ClaimDetectionDto
        {
            Text = claim,
            Type = ResolveType(normalized),
            Confidence = ResolveConfidence(normalized),
            EvidenceType = ResolveEvidenceType(normalized),
            EvidenceTypes = [ResolveEvidenceType(normalized)],
            TrustLevel = EvidenceTrustLevel.ExplicitRegulatoryClaim,
            OriginBlock = "RegulatoryClaimBlock",
            Evidence =
            [
                new SemanticEvidenceDto
                {
                    EvidenceType = ResolveEvidenceType(normalized),
                    Type = ResolveEvidenceType(normalized).ToString(),
                    Text = claim,
                    Confidence = ResolveConfidence(normalized),
                    Source = "explicit_claim",
                    TrustLevel = EvidenceTrustLevel.ExplicitRegulatoryClaim,
                    OriginBlock = "RegulatoryClaimBlock"
                }
            ]
        };
    }

    private static EvidenceType ResolveEvidenceType(string normalized) =>
        normalized.Contains("pode conter", StringComparison.Ordinal) ||
        normalized.Contains("tracos de", StringComparison.Ordinal) ||
        normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
        normalized.Contains("fabricado em linha", StringComparison.Ordinal)
            ? EvidenceType.CrossContamination
            : EvidenceType.ClaimDetected;

    private static string ResolveType(string normalized)
    {
        if (normalized.Contains("pode conter", StringComparison.Ordinal)) return "may_contain";
        if (normalized.StartsWith("tracos de ", StringComparison.Ordinal) || normalized.Contains(" tracos de ", StringComparison.Ordinal)) return "may_contain";
        if (normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) || normalized.Contains("fabricado em linha", StringComparison.Ordinal)) return "cross_contamination";
        if (normalized.StartsWith("alergicos", StringComparison.Ordinal) || normalized.StartsWith("alergenicos", StringComparison.Ordinal)) return "allergen_statement";
        if (normalized.StartsWith("contem ", StringComparison.Ordinal)) return "contains";
        if (normalized.Contains("nao contem gluten", StringComparison.Ordinal) || normalized.Contains("sem gluten", StringComparison.Ordinal)) return "gluten_free";
        if (normalized.Contains("nao contem lactose", StringComparison.Ordinal) || normalized.Contains("sem lactose", StringComparison.Ordinal) || normalized.Contains("zero lactose", StringComparison.Ordinal)) return "lactose_free";
        if (normalized.Contains("zero acucar", StringComparison.Ordinal) || normalized.Contains("sem adicao de acucar", StringComparison.Ordinal) || normalized.Contains("sem acucar", StringComparison.Ordinal)) return "sugar_free";
        if (normalized.Contains("vegano", StringComparison.Ordinal)) return "vegan";
        if (normalized.Contains("vegetariano", StringComparison.Ordinal)) return "vegetarian";
        if (normalized.Contains("plant based", StringComparison.Ordinal)) return "plant_based";
        if (normalized.Contains("organico", StringComparison.Ordinal)) return "organic";
        return "other";
    }

    private static string ResolveConfidence(string normalized)
    {
        if (normalized.Contains("pode conter", StringComparison.Ordinal) ||
            normalized.Contains("tracos de", StringComparison.Ordinal) ||
            normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
            normalized.Contains("fabricado em linha", StringComparison.Ordinal) ||
            normalized.StartsWith("alergicos", StringComparison.Ordinal) ||
            normalized.StartsWith("alergenicos", StringComparison.Ordinal) ||
            normalized.StartsWith("contem ", StringComparison.Ordinal) ||
            normalized.Contains("nao contem", StringComparison.Ordinal) ||
            normalized.Contains("sem ", StringComparison.Ordinal) ||
            normalized.Contains("zero ", StringComparison.Ordinal))
        {
            return "high";
        }

        return "medium";
    }
}
