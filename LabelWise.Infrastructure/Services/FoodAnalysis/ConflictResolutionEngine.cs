using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine de detecção e resolução de conflitos baseado em hierarquia de evidências.
/// </summary>
public sealed class ConflictResolutionEngine : IConflictResolutionEngine
{
    public IReadOnlyList<AnalysisConflict> DetectConflicts(
        IReadOnlyList<RegulatoryClaim> regulatoryClaims,
        IReadOnlyList<Evidence> ingredients,
        IReadOnlyList<Evidence> inferences)
    {
        var conflicts = new List<AnalysisConflict>();

        // 1. Detectar conflitos entre claims regulatórios
        conflicts.AddRange(DetectClaimConflicts(regulatoryClaims));

        // 2. Detectar conflitos entre claims e ingredientes
        conflicts.AddRange(DetectClaimIngredientConflicts(regulatoryClaims, ingredients));

        // 3. Detectar conflitos entre ingredientes e inferências
        conflicts.AddRange(DetectIngredientInferenceConflicts(ingredients, inferences));

        return conflicts;
    }

    public IReadOnlyDictionary<AnalysisConflict, ConflictResolution> ResolveConflicts(
        IReadOnlyList<AnalysisConflict> conflicts)
    {
        var resolutions = new Dictionary<AnalysisConflict, ConflictResolution>();

        foreach (var conflict in conflicts)
        {
            var resolution = ResolveByPriority(conflict);
            resolutions[conflict] = resolution;
        }

        return resolutions;
    }

    public bool IsCriticalConflict(AnalysisConflict conflict)
    {
        return conflict.Severity == ConflictSeverity.Critical;
    }

    public AnalysisQuality EvaluateAnalysisQuality(IReadOnlyList<AnalysisConflict> conflicts)
    {
        if (!conflicts.Any())
            return AnalysisQuality.Reliable;

        var criticalCount = conflicts.Count(c => c.Severity == ConflictSeverity.Critical);
        var moderateCount = conflicts.Count(c => c.Severity == ConflictSeverity.Moderate);

        if (criticalCount > 0)
            return AnalysisQuality.Inconsistent;

        if (moderateCount > 2)
            return AnalysisQuality.Partial;

        if (moderateCount > 0)
            return AnalysisQuality.Partial;

        return AnalysisQuality.Reliable;
    }

    private IEnumerable<AnalysisConflict> DetectClaimConflicts(IReadOnlyList<RegulatoryClaim> claims)
    {
        // Detectar claims contraditórios sobre o mesmo sujeito
        var groupedBySubject = claims.GroupBy(c => c.Subject.ToLowerInvariant());

        foreach (var group in groupedBySubject)
        {
            var claimsList = group.ToList();
            
            // Verificar se há claims positivos e negativos sobre o mesmo sujeito
            var positiveClaims = claimsList.Where(c => c.IsPositiveClaim).ToList();
            var negativeClaims = claimsList.Where(c => !c.IsPositiveClaim).ToList();

            if (positiveClaims.Any() && negativeClaims.Any())
            {
                foreach (var positive in positiveClaims)
                {
                    foreach (var negative in negativeClaims)
                    {
                        // Exceção: "PODE CONTER" não conflita com "SEM"
                        if (positive.IsCrossContamination)
                            continue;

                        yield return new AnalysisConflict
                        {
                            Type = ConflictType.ClaimConflict,
                            Severity = ConflictSeverity.Critical,
                            Description = $"Claims contraditórios: '{positive.OriginalText}' vs '{negative.OriginalText}'",
                            EvidenceA = positive.Evidence,
                            EvidenceB = negative.Evidence,
                            RequiresManualReview = true
                        };
                    }
                }
            }
        }
    }

    private IEnumerable<AnalysisConflict> DetectClaimIngredientConflicts(
        IReadOnlyList<RegulatoryClaim> claims,
        IReadOnlyList<Evidence> ingredients)
    {
        foreach (var claim in claims)
        {
            // Se o claim diz "SEM X" mas X está nos ingredientes
            if (!claim.IsPositiveClaim && claim.ClaimType == RegulatoryClaimType.FreeFrom)
            {
                foreach (var ingredient in ingredients)
                {
                    if (IngredientContainsSubject(ingredient.Text, claim.Subject))
                    {
                        yield return new AnalysisConflict
                        {
                            Type = ConflictType.ClaimIngredientMismatch,
                            Severity = ConflictSeverity.Critical,
                            Description = $"Claim '{claim.OriginalText}' contradiz ingrediente '{ingredient.Text}'",
                            EvidenceA = claim.Evidence,
                            EvidenceB = ingredient,
                            RequiresManualReview = true
                        };
                    }
                }
            }

            // Se o claim diz "CONTÉM X" mas X não está nos ingredientes explícitos
            if (claim.IsPositiveClaim && 
                claim.ClaimType == RegulatoryClaimType.Contains &&
                !claim.IsCrossContamination)
            {
                var foundInIngredients = ingredients.Any(i => 
                    IngredientContainsSubject(i.Text, claim.Subject));

                if (!foundInIngredients && ingredients.Any())
                {
                    // Este é um conflito moderado - o claim pode estar correto mas ingrediente não foi detectado
                    yield return new AnalysisConflict
                    {
                        Type = ConflictType.ClaimIngredientMismatch,
                        Severity = ConflictSeverity.Moderate,
                        Description = $"Claim '{claim.OriginalText}' não encontra correspondência nos ingredientes detectados",
                        EvidenceA = claim.Evidence,
                        EvidenceB = ingredients.FirstOrDefault() ?? claim.Evidence,
                        RequiresManualReview = false,
                        Resolution = "Priorizando claim regulatório sobre lista de ingredientes detectada"
                    };
                }
            }
        }
    }

    private IEnumerable<AnalysisConflict> DetectIngredientInferenceConflicts(
        IReadOnlyList<Evidence> ingredients,
        IReadOnlyList<Evidence> inferences)
    {
        foreach (var inference in inferences)
        {
            // Inferências que contradizem ingredientes explícitos
            var conflictingIngredients = ingredients.Where(ing =>
                AreContradictory(ing.Text, inference.Text)).ToList();

            foreach (var ingredient in conflictingIngredients)
            {
                yield return new AnalysisConflict
                {
                    Type = ConflictType.MultiSourceConflict,
                    Severity = ConflictSeverity.Moderate,
                    Description = $"Inferência '{inference.Text}' contradiz ingrediente explícito '{ingredient.Text}'",
                    EvidenceA = ingredient,
                    EvidenceB = inference,
                    RequiresManualReview = false,
                    Resolution = "Priorizando ingrediente explícito sobre inferência"
                };
            }
        }
    }

    private ConflictResolution ResolveByPriority(AnalysisConflict conflict)
    {
        var evidenceA = conflict.EvidenceA;
        var evidenceB = conflict.EvidenceB;

        // Aplicar hierarquia de prioridade
        if (evidenceA.Priority > evidenceB.Priority)
        {
            return new ConflictResolution
            {
                WinningEvidence = evidenceA,
                DiscardedEvidence = evidenceB,
                Reason = $"Prioridade {evidenceA.Priority} > {evidenceB.Priority}",
                ConfidenceImpact = CalculateConfidenceImpact(conflict.Severity)
            };
        }
        else if (evidenceB.Priority > evidenceA.Priority)
        {
            return new ConflictResolution
            {
                WinningEvidence = evidenceB,
                DiscardedEvidence = evidenceA,
                Reason = $"Prioridade {evidenceB.Priority} > {evidenceA.Priority}",
                ConfidenceImpact = CalculateConfidenceImpact(conflict.Severity)
            };
        }
        else
        {
            // Mesma prioridade - usar confiança
            if (evidenceA.Confidence >= evidenceB.Confidence)
            {
                return new ConflictResolution
                {
                    WinningEvidence = evidenceA,
                    DiscardedEvidence = evidenceB,
                    Reason = $"Confiança {evidenceA.Confidence:F2} >= {evidenceB.Confidence:F2}",
                    ConfidenceImpact = CalculateConfidenceImpact(conflict.Severity)
                };
            }
            else
            {
                return new ConflictResolution
                {
                    WinningEvidence = evidenceB,
                    DiscardedEvidence = evidenceA,
                    Reason = $"Confiança {evidenceB.Confidence:F2} > {evidenceA.Confidence:F2}",
                    ConfidenceImpact = CalculateConfidenceImpact(conflict.Severity)
                };
            }
        }
    }

    private static double CalculateConfidenceImpact(ConflictSeverity severity)
    {
        return severity switch
        {
            ConflictSeverity.Critical => -0.40,
            ConflictSeverity.Moderate => -0.20,
            ConflictSeverity.Minor => -0.05,
            _ => 0.0
        };
    }

    private static bool IngredientContainsSubject(string ingredientText, string subject)
    {
        var normalized = ingredientText.ToLowerInvariant();
        var normalizedSubject = subject.ToLowerInvariant();

        // Mapear variações
        var variations = GetSubjectVariations(normalizedSubject);
        
        return variations.Any(v => normalized.Contains(v));
    }

    private static IEnumerable<string> GetSubjectVariations(string subject)
    {
        yield return subject;

        // Variações conhecidas
        var mappings = new Dictionary<string, string[]>
        {
            { "glúten", new[] { "gluten", "trigo", "cevada", "centeio", "malte" } },
            { "lactose", new[] { "lactose", "leite", "lactico" } },
            { "leite", new[] { "leite", "lactose", "lactico", "creme de leite", "soro de leite" } },
            { "ovo", new[] { "ovo", "ovos", "albumina" } }
        };

        if (mappings.TryGetValue(subject, out var variations))
        {
            foreach (var variation in variations)
                yield return variation;
        }
    }

    private static bool AreContradictory(string textA, string textB)
    {
        // Lógica simples - pode ser expandida
        var normalizedA = textA.ToLowerInvariant();
        var normalizedB = textB.ToLowerInvariant();

        // Exemplo: "sem açúcar" vs "contém açúcar"
        var containsNegation = normalizedA.Contains("sem") || normalizedA.Contains("não") ||
                               normalizedB.Contains("sem") || normalizedB.Contains("não");

        if (!containsNegation)
            return false;

        // Se ambos falam do mesmo assunto, são contraditórios
        var commonTerms = new[] { "açúcar", "glúten", "lactose", "leite", "ovo" };
        
        return commonTerms.Any(term => 
            normalizedA.Contains(term) && normalizedB.Contains(term));
    }
}
