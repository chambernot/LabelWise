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
    }
}