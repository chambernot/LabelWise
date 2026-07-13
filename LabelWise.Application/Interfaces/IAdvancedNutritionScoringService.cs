using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço de scoring nutricional avançado com suporte a múltiplos perfis de saúde.
    /// Implementa 5 blocos de avaliação: qualidade nutricional, nível de processamento,
    /// adequação ao perfil, avaliação de claims e densidade nutricional.
    /// </summary>
    public interface IAdvancedNutritionScoringService
    {
        /// <summary>
        /// Calcula o score nutricional avançado baseado no perfil nutricional estimado,
        /// lista de ingredientes e claims visíveis do produto.
        /// </summary>
        /// <param name="profile">Perfil nutricional por 100g/ml.</param>
        /// <param name="ingredients">Lista de ingredientes do produto (pode ser nula).</param>
        /// <param name="visibleClaims">Claims visíveis na embalagem (ex: "Zero açúcar", "Light").</param>
        /// <returns>Resultado completo com scores gerais e por perfil.</returns>
        AdvancedNutritionScoreResult Calculate(
            EstimatedNutritionProfileDto? profile,
            IReadOnlyList<string>? ingredients,
            IReadOnlyList<string>? visibleClaims);
    }
}
