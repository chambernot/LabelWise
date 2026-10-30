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

        // Modo GLP-1: mesmo status, com as sugestões do GLP-1 (meta de proteína do Glp1Profiles, restrições e aversões)
        Task<DailyStatusResponseDto> GetDailyStatusAndSuggestionAsync(string userId, DateTime date, int glp1ProteinTargetG);

        // Mesmo cálculo do status do dia, sem gerar sugestões com IA (não consome chamada de IA)
        Task<DailyStatusResponseDto> GetDailyStatusAsync(string userId, DateTime date);

        Task SetUserGoalAsync(SetNutritionGoalDto request);

        Task<List<MealLog>> GetDailyMealsAsync(string userId, DateTime date); // ◄◄ Método adicionado

        Task DeleteMealAsync(string mealId); // ◄◄ Método adicionado

    }
}