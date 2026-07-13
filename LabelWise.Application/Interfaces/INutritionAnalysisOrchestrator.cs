using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Orquestra o pipeline completo de análise nutricional a partir dos bytes da imagem.
///
/// Pipeline:
///   1. Pré-processamento seguro da imagem (apenas para etapas internas quando necessário)
///   2. Detecção de código de barras → OpenFoodFacts (quando disponível)
///   3. Document Intelligence → extração de tabela nutricional
///   4. INutritionValidator.Validate()
///   5. INutritionEnricher.Enrich()
///   6. INutritionScoringService.Calculate()
///   7. AdvancedNutritionProfileEvaluator.Evaluate()
///   8. INutritionResponseBuilder.Build()
///
/// O Controller APENAS chama este orquestrador e retorna Ok(response).
/// </summary>
public interface INutritionAnalysisOrchestrator
{
    Task<UnifiedNutritionAnalysisResponse> AnalyzeAsync(
        byte[] rawImageBytes,
        string? mimeType,
        CancellationToken cancellationToken = default);
}
