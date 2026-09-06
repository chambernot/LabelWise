using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Entities.Nutrition;

namespace LabelWise.Application.Interfaces.Persistence;


public interface INutritionRepository
{
    Task SalvarClarificacaoPendenteAsync(MealClarificationContext context);
    Task<MealClarificationContext?> ObterClarificacaoPendenteAsync(string userId);
    Task RemoverClarificacaoPendenteAsync(string userId);
    Task InserirMealLogAsync(MealLog mealLog);
    Task<DailyNutritionGoal> ObterMetaDiariaAsync(string userId, DateTime data);

    Task<List<MealLog>> ObterRefeicoesDoDiaAsync(string userId, DateTime data); // ◄◄ Método adicionado

    Task SalvarMetaDiariaAsync(DailyNutritionGoal goal);

    Task ExcluirMealLogAsync(string id); // ◄◄ Método adicionado

    Task InserirPacienteAsync(PatientDto paciente);
    Task<List<PatientDto>> ObterPacientesPorProfissionalAsync(string professionalId);

}