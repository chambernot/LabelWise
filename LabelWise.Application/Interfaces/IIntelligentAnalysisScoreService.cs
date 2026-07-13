using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Calcula o score nutricional consumido pelo endpoint
/// <c>nutricao-analise-inteligente</c>. Encapsula toda a regra de
/// montagem do <see cref="NutritionEnrichedData"/> e o mapeamento do
/// <see cref="UnifiedNutritionScore"/> para o contrato do front
/// (<see cref="ScoreSection"/>), mantendo a controller livre de regra
/// de negócio.
/// </summary>
public interface IIntelligentAnalysisScoreService
{
    /// <summary>
    /// Atribui <c>response.Score</c> a partir de <c>response.Nutrition.Per100</c>.
    /// Não faz nada se a base per100 não estiver disponível.
    /// </summary>
    /// <param name="response">Resposta unificada que terá o campo Score populado.</param>
    /// <param name="confidence">
    /// Resultado do <see cref="INutritionConfidenceEngine"/> quando a fonte
    /// for OpenAI Vision; null para fontes determinísticas (ex.: Open Food Facts).
    /// </param>
    void Apply(IntelligentAnalysisResponse response, NutritionConfidenceResult? confidence);
}
