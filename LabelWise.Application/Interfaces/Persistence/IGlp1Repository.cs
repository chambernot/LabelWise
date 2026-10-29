using LabelWise.Domain.Entities.Glp1;

namespace LabelWise.Application.Interfaces.Persistence;

// Coleções do Modo GLP-1 (Glp1Profiles, Glp1SymptomLogs, Glp1Alerts).
// Água e proteína do dia vêm das coleções que já existem (WaterLogs e Nutrition_MealLogs), só para leitura.
public interface IGlp1Repository
{
    Task<Glp1Profile?> ObterPerfilAsync(string userId);
    Task<int> ContarSintomasAsync(string userId);
    Task<int> ContarAlertasAsync(string userId);

    Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc);
    Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc);

    // Apaga perfil, sintomas e alertas do telefone. Não mexe em WaterLogs, WeightLogs nem refeições.
    Task ApagarDadosAsync(string userId);
}
