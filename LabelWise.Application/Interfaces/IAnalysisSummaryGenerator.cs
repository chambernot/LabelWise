using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;
using System.Collections.Generic;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Interface para geração de resumos de análise de produtos.
    /// Permite implementações baseadas em regras ou IA generativa.
    /// </summary>
    public interface IAnalysisSummaryGenerator
    {
        /// <summary>
        /// Gera um resumo textual da análise do produto.
        /// </summary>
        /// <param name="product">Produto analisado</param>
        /// <param name="nutrition">Informações nutricionais</param>
        /// <param name="ingredients">Lista de ingredientes</param>
        /// <param name="allergens">Lista de alérgenos</param>
        /// <param name="userProfile">Perfil do usuário (opcional)</param>
        /// <param name="generalScore">Score geral calculado (0-1)</param>
        /// <param name="personalizedScore">Score personalizado calculado (0-1)</param>
        /// <param name="alerts">Lista de alertas gerados</param>
        /// <param name="recommendations">Lista de recomendações geradas</param>
        /// <returns>Resumo textual da análise</returns>
        string GenerateSummary(
            Product product,
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile,
            double generalScore,
            double personalizedScore,
            List<string> alerts,
            List<string> recommendations);

        /// <summary>
        /// Nome da estratégia de geração (ex: "RuleBased", "OpenAI", "AzureAI")
        /// </summary>
        string StrategyName { get; }
    }
}
