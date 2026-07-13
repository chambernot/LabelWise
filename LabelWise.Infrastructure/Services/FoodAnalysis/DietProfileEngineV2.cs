using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine de avaliação de perfis alimentares baseado em evidências hierárquicas.
/// Respeita ABSOLUTAMENTE a prioridade de claims regulatórios sobre inferências.
/// </summary>
public sealed class DietProfileEngineV2
{
    private readonly IngredientKnowledgeBase _knowledgeBase;

    public DietProfileEngineV2(IngredientKnowledgeBase knowledgeBase)
    {
        _knowledgeBase = knowledgeBase;
    }

    public ProfileCompatibility EvaluateGlutenFree(
        IReadOnlyList<RegulatoryClaim> claims,
        IReadOnlyList<Evidence> ingredients,
        IReadOnlyList<Evidence> inferences)
    {
        // 1. PRIORIDADE ABSOLUTA: Claims regulatórios
        var glutenClaims = claims.Where(c =>
            c.Subject.Contains("glúten", StringComparison.OrdinalIgnoreCase) ||
            c.Subject.Contains("gluten", StringComparison.OrdinalIgnoreCase) ||
            c.Subject.Contains("trigo", StringComparison.OrdinalIgnoreCase)).ToList();

        // Claim "CONTÉM GLÚTEN" = INCOMPATÍVEL (absoluto)
        var containsClaim = glutenClaims.FirstOrDefault(c =>
            c.ClaimType == RegulatoryClaimType.Contains && c.IsPositiveClaim);
        
        if (containsClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "GlutenFree",
                Status = FoodCompatibilityStatus.Incompatible,
                Confidence = 1.0,
                Reasons = [$"Claim regulatório: '{containsClaim.OriginalText}'"],
                Warnings = ["Produto contém glúten conforme declaração regulatória."],
                SupportingEvidence = [containsClaim.Evidence]
            };
        }

        // Claim "PODE CONTER GLÚTEN" = RISCO DE CONTAMINAÇÃO
        var mayContainClaim = glutenClaims.FirstOrDefault(c =>
            c.IsCrossContamination);
        
        if (mayContainClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "GlutenFree",
                Status = FoodCompatibilityStatus.CrossContaminationRisk,
                Confidence = 0.95,
                Reasons = [$"Risco de contaminação cruzada: '{mayContainClaim.OriginalText}'"],
                Warnings = ["Produto pode conter traços de glúten por contaminação cruzada."],
                SupportingEvidence = [mayContainClaim.Evidence]
            };
        }

        // Claim "SEM GLÚTEN" = COMPATÍVEL (absoluto)
        var freeFromClaim = glutenClaims.FirstOrDefault(c =>
            c.ClaimType == RegulatoryClaimType.FreeFrom && !c.IsPositiveClaim);
        
        if (freeFromClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "GlutenFree",
                Status = FoodCompatibilityStatus.Compatible,
                Confidence = 1.0,
                Reasons = [$"Claim regulatório confirmado: '{freeFromClaim.OriginalText}'"],
                Warnings = [],
                SupportingEvidence = [freeFromClaim.Evidence]
            };
        }

        // 2. INGREDIENTES EXPLÍCITOS (prioridade alta)
        var glutenIngredients = ingredients.Where(ing =>
            _knowledgeBase.ContainsGluten(ing.Text) ||
            ContainsGlutenSource(ing.Text)).ToList();

        if (glutenIngredients.Any())
        {
            return new ProfileCompatibility
            {
                ProfileName = "GlutenFree",
                Status = FoodCompatibilityStatus.Incompatible,
                Confidence = 0.9,
                Reasons = glutenIngredients.Select(ing => 
                    $"Ingrediente contém glúten: '{ing.Text}'").ToList(),
                Warnings = ["Ingredientes detectados indicam presença de glúten."],
                SupportingEvidence = glutenIngredients.ToList()
            };
        }

        // 3. INFERÊNCIAS (prioridade baixa)
        var glutenInferences = inferences.Where(inf =>
            inf.Text.Contains("glúten", StringComparison.OrdinalIgnoreCase) ||
            inf.Text.Contains("trigo", StringComparison.OrdinalIgnoreCase)).ToList();

        if (glutenInferences.Any())
        {
            var avgConfidence = glutenInferences.Average(i => i.Confidence);
            
            if (avgConfidence >= 0.7)
            {
                return new ProfileCompatibility
                {
                    ProfileName = "GlutenFree",
                    Status = FoodCompatibilityStatus.LikelyIncompatible,
                    Confidence = avgConfidence,
                    Reasons = ["Análise semântica sugere presença de glúten"],
                    Warnings = ["Baseado em inferência - não há confirmação regulatória."],
                    SupportingEvidence = glutenInferences.ToList()
                };
            }
        }

        // 4. DADOS INSUFICIENTES
        if (!ingredients.Any() && !inferences.Any())
        {
            return new ProfileCompatibility
            {
                ProfileName = "GlutenFree",
                Status = FoodCompatibilityStatus.InsufficientData,
                Confidence = 0.0,
                Reasons = ["Informações insuficientes para determinar presença de glúten"],
                Warnings = ["Qualidade de OCR baixa ou informações incompletas."],
                SupportingEvidence = []
            };
        }

        // 5. PROVAVELMENTE COMPATÍVEL (sem evidência contrária)
        return new ProfileCompatibility
        {
            ProfileName = "GlutenFree",
            Status = FoodCompatibilityStatus.LikelyCompatible,
            Confidence = 0.6,
            Reasons = ["Nenhuma evidência de glúten detectada na análise"],
            Warnings = ["Sem confirmação regulatória - ausência de detecção não garante ausência."],
            SupportingEvidence = []
        };
    }

    public ProfileCompatibility EvaluateLactoseFree(
        IReadOnlyList<RegulatoryClaim> claims,
        IReadOnlyList<Evidence> ingredients,
        IReadOnlyList<Evidence> inferences)
    {
        // 1. Claims regulatórios
        var lactoseClaims = claims.Where(c =>
            c.Subject.Contains("lactose", StringComparison.OrdinalIgnoreCase) ||
            c.Subject.Contains("leite", StringComparison.OrdinalIgnoreCase)).ToList();

        var containsClaim = lactoseClaims.FirstOrDefault(c =>
            c.ClaimType == RegulatoryClaimType.Contains && c.IsPositiveClaim);
        
        if (containsClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "LactoseFree",
                Status = FoodCompatibilityStatus.Incompatible,
                Confidence = 1.0,
                Reasons = [$"Claim regulatório: '{containsClaim.OriginalText}'"],
                Warnings = ["Produto contém lactose conforme declaração regulatória."],
                SupportingEvidence = [containsClaim.Evidence]
            };
        }

        var mayContainClaim = lactoseClaims.FirstOrDefault(c => c.IsCrossContamination);
        if (mayContainClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "LactoseFree",
                Status = FoodCompatibilityStatus.CrossContaminationRisk,
                Confidence = 0.95,
                Reasons = [$"Risco de contaminação: '{mayContainClaim.OriginalText}'"],
                Warnings = ["Produto pode conter traços de lactose."],
                SupportingEvidence = [mayContainClaim.Evidence]
            };
        }

        var freeFromClaim = lactoseClaims.FirstOrDefault(c =>
            c.ClaimType == RegulatoryClaimType.FreeFrom && !c.IsPositiveClaim);
        
        if (freeFromClaim != null)
        {
            return new ProfileCompatibility
            {
                ProfileName = "LactoseFree",
                Status = FoodCompatibilityStatus.Compatible,
                Confidence = 1.0,
                Reasons = [$"Claim regulatório: '{freeFromClaim.OriginalText}'"],
                Warnings = [],
                SupportingEvidence = [freeFromClaim.Evidence]
            };
        }

        // 2. Ingredientes explícitos
        var lactoseIngredients = ingredients.Where(ing =>
            _knowledgeBase.ContainsLactose(ing.Text) ||
            ContainsLactoseSource(ing.Text)).ToList();

        if (lactoseIngredients.Any())
        {
            return new ProfileCompatibility
            {
                ProfileName = "LactoseFree",
                Status = FoodCompatibilityStatus.Incompatible,
                Confidence = 0.9,
                Reasons = lactoseIngredients.Select(ing =>
                    $"Ingrediente contém lactose: '{ing.Text}'").ToList(),
                Warnings = ["Ingredientes indicam presença de lactose."],
                SupportingEvidence = lactoseIngredients.ToList()
            };
        }

        // 3. Inferências
        var lactoseInferences = inferences.Where(inf =>
            inf.Text.Contains("lactose", StringComparison.OrdinalIgnoreCase) ||
            inf.Text.Contains("leite", StringComparison.OrdinalIgnoreCase)).ToList();

        if (lactoseInferences.Any())
        {
            var avgConfidence = lactoseInferences.Average(i => i.Confidence);
            
            if (avgConfidence >= 0.7)
            {
                return new ProfileCompatibility
                {
                    ProfileName = "LactoseFree",
                    Status = FoodCompatibilityStatus.LikelyIncompatible,
                    Confidence = avgConfidence,
                    Reasons = ["Análise semântica sugere presença de lactose"],
                    Warnings = ["Baseado em inferência - não há confirmação regulatória."],
                    SupportingEvidence = lactoseInferences.ToList()
                };
            }
        }

        if (!ingredients.Any() && !inferences.Any())
        {
            return new ProfileCompatibility
            {
                ProfileName = "LactoseFree",
                Status = FoodCompatibilityStatus.InsufficientData,
                Confidence = 0.0,
                Reasons = ["Informações insuficientes"],
                Warnings = ["Qualidade de OCR baixa."],
                SupportingEvidence = []
            };
        }

        return new ProfileCompatibility
        {
            ProfileName = "LactoseFree",
            Status = FoodCompatibilityStatus.LikelyCompatible,
            Confidence = 0.6,
            Reasons = ["Nenhuma evidência de lactose detectada"],
            Warnings = ["Sem confirmação regulatória."],
            SupportingEvidence = []
        };
    }

    private static bool ContainsGlutenSource(string text)
    {
        var glutenSources = new[] { "trigo", "cevada", "centeio", "malte", "farinha de trigo" };
        return glutenSources.Any(source => 
            text.Contains(source, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsLactoseSource(string text)
    {
        var lactoseSources = new[] { "leite", "lactose", "queijo", "manteiga", "creme", "soro", "whey", "iogurte" };
        return lactoseSources.Any(source =>
            text.Contains(source, StringComparison.OrdinalIgnoreCase));
    }
}
