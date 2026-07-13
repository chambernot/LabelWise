using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Implementação futura usando IA Generativa (OpenAI, Azure OpenAI, etc).
    /// Atualmente retorna um placeholder preparado para integração.
    /// </summary>
    public class AiSummaryGenerator : IAnalysisSummaryGenerator
    {
        private readonly IAiProviderService? _aiProvider;

        public string StrategyName => "AI-Powered";

        // Constructor com provider opcional - permite uso sem provider configurado
        public AiSummaryGenerator(IAiProviderService? aiProvider = null)
        {
            _aiProvider = aiProvider;
        }

        public string GenerateSummary(
            Product product,
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile,
            double generalScore,
            double personalizedScore,
            List<string> alerts,
            List<string> recommendations)
        {
            // Se não há provider configurado, retorna placeholder educativo
            if (_aiProvider == null)
            {
                return GeneratePlaceholderSummary(generalScore, personalizedScore);
            }

            // TODO: Implementar chamada real para IA generativa
            // var prompt = BuildPrompt(product, nutrition, ingredients, allergens, userProfile, generalScore, personalizedScore, alerts, recommendations);
            // var aiSummary = await _aiProvider.GenerateCompletionAsync(prompt);
            // return aiSummary;

            // Por enquanto, retorna fallback
            return GeneratePlaceholderSummary(generalScore, personalizedScore);
        }

        private string GeneratePlaceholderSummary(double generalScore, double personalizedScore)
        {
            var avgScore = (generalScore + personalizedScore) / 2.0;
            return $"[AI Mode - Aguardando Implementação] Score: {avgScore:P0}";
        }

        /// <summary>
        /// Constrói o prompt estruturado para a IA generativa.
        /// </summary>
        private string BuildPrompt(
            Product product,
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile,
            double generalScore,
            double personalizedScore,
            List<string> alerts,
            List<string> recommendations)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine("Você é um especialista em análise nutricional. Gere um resumo conciso e informativo do produto abaixo:");
            prompt.AppendLine();
            prompt.AppendLine($"**Produto:** {product.Name}");
            
            if (!string.IsNullOrEmpty(product.Brand))
                prompt.AppendLine($"**Marca:** {product.Brand}");

            prompt.AppendLine();
            prompt.AppendLine($"**Scores:** Geral: {generalScore:P0}, Personalizado: {personalizedScore:P0}");

            if (nutrition != null)
            {
                prompt.AppendLine();
                prompt.AppendLine("**Informações Nutricionais:**");
                prompt.AppendLine($"- Calorias: {nutrition.Calories} kcal");
                prompt.AppendLine($"- Proteínas: {nutrition.ProteinGrams}g");
                prompt.AppendLine($"- Carboidratos: {nutrition.TotalCarbohydratesGrams}g");
                prompt.AppendLine($"- Açúcares: {nutrition.SugarsGrams}g");
                prompt.AppendLine($"- Gorduras: {nutrition.TotalFatGrams}g");
                prompt.AppendLine($"- Gorduras Saturadas: {nutrition.SaturatedFatGrams}g");
                prompt.AppendLine($"- Fibras: {nutrition.DietaryFiberGrams}g");
                prompt.AppendLine($"- Sódio: {nutrition.SodiumMg}mg");
            }

            if (alerts.Any())
            {
                prompt.AppendLine();
                prompt.AppendLine("**Alertas:**");
                foreach (var alert in alerts)
                {
                    prompt.AppendLine($"- {alert}");
                }
            }

            if (recommendations.Any())
            {
                prompt.AppendLine();
                prompt.AppendLine("**Recomendações:**");
                foreach (var rec in recommendations)
                {
                    prompt.AppendLine($"- {rec}");
                }
            }

            if (userProfile != null)
            {
                prompt.AppendLine();
                prompt.AppendLine("**Perfil do Usuário:**");
                prompt.AppendLine($"- Objetivo: {userProfile.Goal}");

                var restrictions = new List<string>();
                if (userProfile.LactoseIntolerance) restrictions.Add("Intolerância à lactose");
                if (userProfile.GlutenFree) restrictions.Add("Sem glúten");
                if (userProfile.Diabetes) restrictions.Add("Diabetes");
                if (userProfile.SodiumControl) restrictions.Add("Controle de sódio");
                if (!string.IsNullOrEmpty(userProfile.OtherRestrictions)) 
                    restrictions.Add(userProfile.OtherRestrictions);

                if (restrictions.Any())
                    prompt.AppendLine($"- Restrições: {string.Join(", ", restrictions)}");
            }

            prompt.AppendLine();
            prompt.AppendLine("Gere um resumo em português, conciso (máximo 3 linhas), destacando os pontos mais importantes para o consumidor.");

            return prompt.ToString();
        }
    }

    /// <summary>
    /// Interface para abstração de provedores de IA (OpenAI, Azure OpenAI, etc)
    /// </summary>
    public interface IAiProviderService
    {
        Task<string> GenerateCompletionAsync(string prompt);
    }
}
