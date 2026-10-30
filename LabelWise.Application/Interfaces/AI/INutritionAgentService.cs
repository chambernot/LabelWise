using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Entities.Nutrition; // 👈 Certifique-se de ter este using
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces.AI
{
    public interface INutritionAgentService
    {
        // glp1 preenchido = sugestões do Modo GLP-1 (no máximo 2, porção pequena, proteína). Nulo = comportamento de sempre.
        Task<List<string>> GenerateProactiveSuggestionsAsync(MacroSummaryDto remainingBalance, string nextMealType, List<string>? pratosJaConsumidos = null, Glp1SuggestionContext? glp1 = null);

        Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request);

        // 🚀 Adicione esta linha para aceitar o histórico de conversas na interface
        Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request, List<ChatMessageLog>? chatHistory);

        Task<ExtractDietGoalResponseDto> ExtractDietGoalsFromDocumentAsync(ExtractDietGoalRequestDto request);

        Task<string> GenerateDailyFeedbackMessageAsync(
    string patientGoal,
    int targetCalories,
    int consumedCalories,
    decimal targetProtein,
    decimal consumedProtein,
    List<string> mealsLogged);

        // No contrato INutritionAgentService.cs (adicione a assinatura)
        // modoGlp1 = true anexa as regras de segurança do Modo GLP-1 ao prompt
        Task<string> GenerateSmartSubstitutionAsync(string foodToSubstitute, MacroSummaryDto remainingBalance, string clinicalProtocol, string foodAversions, bool modoGlp1 = false);

        // Assinatura no INutritionAgentService.cs
        Task<string> GenerateWeeklyProgressSummaryAsync(string patientName, decimal initialWeight, decimal currentWeight, int targetCalories, double avgConsumedCalories, int avgWaterMl, int streakDays);

        Task<AutoCalculateGoalDto> CalculateNutritionalGoalsAsync(string userProfileText, string mainGoal);


    }
}