using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Valida e sanitiza o perfil nutricional bruto vindo do OCR.
///
/// Responsabilidades:
///   - Remover valores fora de faixa plausível (0–900 kcal, 0–100g macros, etc.)
///   - Corrigir inconsistências estruturais (açúcar > carboidratos, gordura sat > gordura total)
///   - Detectar inconsistência calórica (sinaliza, não corrige)
///   - Emitir avisos de validação
///
/// NÃO aplica fallback, NÃO determina processamento, NÃO calcula score.
/// </summary>
public interface INutritionValidator
{
    NutritionSanitizationResult Validate(EstimatedNutritionProfileDto? profile);
}
