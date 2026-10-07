using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Entities.Nutrition; // 👈 Certifique-se de ter este using
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces.AI
{
    public interface INutritionAgentService
    {
        Task<List<string>> GenerateProactiveSuggestionsAsync(MacroSummaryDto remainingBalance, string nextMealType, List<string>? pratosJaConsumidos = null);

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
        Task<string> GenerateSmartSubstitutionAsync(string foodToSubstitute, MacroSummaryDto remainingBalance, string clinicalProtocol, string foodAversions);

        // Assinatura no INutritionAgentService.cs
        Task<string> GenerateWeeklyProgressSummaryAsync(string patientName, decimal initialWeight, decimal currentWeight, int targetCalories, double avgConsumedCalories, int avgWaterMl, int streakDays);
    }
}