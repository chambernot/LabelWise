using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// State Machine determinística da análise nutricional.
///
/// É a ÚNICA fonte de verdade para:
///   • flags <c>HasNutritionTable</c>, <c>HasMinimumNutritionData</c>
///   • <c>NutritionDataQuality</c> e <c>AnalysisMode</c>
///   • modo de baixa confiança
///   • label / valor exibido do score quando os dados não permitem cálculo
///   • textos dos perfis de usuário em estados não-completos
///
/// Nenhuma outra lógica deve sobrescrever as decisões aplicadas por <see cref="Apply"/>.
/// </summary>
public interface INutritionStateMachine
{
    /// <summary>Determina o estado a partir do contexto de extração.</summary>
    NutritionAnalysisState DetermineState(NutritionContext context);

    /// <summary>
    /// Aplica o estado determinístico sobre a resposta final, ajustando flags,
    /// score (quando aplicável) e perfis de usuário.
    /// </summary>
    void Apply(NutritionAnalysisState state, UnifiedNutritionAnalysisResponse response);
}
