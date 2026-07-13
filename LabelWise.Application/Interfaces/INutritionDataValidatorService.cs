using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço responsável por validar, normalizar e enriquecer os dados nutricionais
    /// retornados pela IA antes do cálculo de score.
    ///
    /// Regra de ouro: dados válidos da IA nunca são sobrescritos.
    /// O backend corrige apenas valores nulos, impossíveis ou inconsistentes.
    /// </summary>
    public interface INutritionDataValidatorService
    {
        /// <summary>
        /// Valida e enriquece o perfil nutricional da IA.
        /// </summary>
        /// <param name="profile">Perfil nutricional retornado pela IA (pode ser nulo).</param>
        /// <param name="category">Categoria do produto (usada para fallback e nível de processamento).</param>
        /// <param name="analysisMode">Modo de análise (FrontOfPackageOnly aciona fallback automático).</param>
        /// <param name="ingredients">Lista de ingredientes (usada para detectar ultraprocessados).</param>
        /// <returns>Dados enriquecidos com perfil normalizado, fallback e confiança.</returns>
        NutritionEnrichedData ValidateAndEnrich(
            EstimatedNutritionProfileDto? profile,
            string? category,
            AnalysisMode analysisMode,
            IReadOnlyList<string>? ingredients);
    }
}
