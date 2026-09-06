namespace LabelWise.Application.Interfaces.AI;

using LabelWise.Application.DTOs.Nutrition;

public interface INutritionAgentService
{
    Task<MealAnalysisResponseDto> ExtractMealDataAsync(
        ParseMealRequestDto request);
    
    Task<List<string>> GenerateProactiveSuggestionsAsync(
            MacroSummaryDto remainingBalance,
            string nextMealType,
            List<string>? pratosJaConsumidos = null);
}