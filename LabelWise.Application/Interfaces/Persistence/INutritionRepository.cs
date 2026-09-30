using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Entities.Nutrition;

namespace LabelWise.Application.Interfaces.Persistence;


public interface INutritionRepository
{
    Task<List<string>> ObterTelefonesAtivosAsync();
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

    Task<int> CalcularOfensivaStreakAsync(string userId);

    // 🚀 Novas assinaturas para o Histórico de Conversa (Memória de Curto Prazo)
    Task SalvarMensagemHistoricoAsync(string userId, string role, string content);
    Task<List<ChatMessageLog>> ObterUltimasMensagensAsync(string userId, int limite = 6);

}