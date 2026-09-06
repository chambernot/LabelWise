using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Entities.Nutrition;
using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface INutritionService
    {
        Task<MealAnalysisResponseDto> ProcessMealEntryAsync(ParseMealRequestDto request);
        Task<DailyStatusResponseDto> GetDailyStatusAndSuggestionAsync(string userId, DateTime date);

        Task SetUserGoalAsync(SetNutritionGoalDto request);

        Task<List<MealLog>> GetDailyMealsAsync(string userId, DateTime date); // ◄◄ Método adicionado

        Task DeleteMealAsync(string mealId); // ◄◄ Método adicionado

    }
}