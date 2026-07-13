using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Validador híbrido que usa Azure Computer Vision para validar valores críticos
/// extraídos pelo Azure OpenAI Vision.
/// </summary>
public interface IHybridOcrValidator
{
    /// <summary>
    /// Valida e corrige valores nutricionais usando OCR de precisão (Azure Computer Vision)
    /// quando detecta divergências com os valores extraídos pela IA.
    /// </summary>
    /// <param name="profile">Perfil nutricional extraído pela IA</param>
    /// <param name="imagePath">Caminho da imagem original</param>
    /// <param name="warnings">Lista de warnings para adicionar mensagens de correção</param>
    /// <returns>True se houve correção, false caso contrário</returns>
    Task<bool> ValidateAndCorrectAsync(
        EstimatedNutritionProfileDto profile,
        string imagePath,
        List<string> warnings);
}
