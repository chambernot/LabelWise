using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Gera um resumo textual baseado em sinais nutricionais do perfil.
/// Regras genéricas — sem heurísticas por produto ou categoria específica.
/// </summary>
public sealed class SummaryService : ISummaryService
{
    public string Generate(NutritionProfile profile)
    {
        if (profile.Fat >= 40)
            return "Produto com alto teor de gordura e gordura saturada";

        if (profile.Sugar >= 15)
            return "Produto com alto teor de açúcar";

        if (profile.Sodium >= 600)
            return "Produto com alto teor de sódio";

        if (profile.Protein >= 15)
            return "Produto com alto teor proteico";

        if (profile.Fiber >= 6)
            return "Produto com alto teor de fibras alimentares";

        if (profile.Calories >= 400)
            return "Produto com alta densidade calórica";

        return "Produto com perfil nutricional moderado";
    }
}
