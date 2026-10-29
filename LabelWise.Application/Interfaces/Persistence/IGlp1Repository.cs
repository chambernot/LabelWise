using LabelWise.Domain.Entities.Glp1;

namespace LabelWise.Application.Interfaces.Persistence;

// Coleções do Modo GLP-1 (Glp1Profiles, Glp1SymptomLogs, Glp1Alerts).
// Água e proteína do dia vêm das coleções que já existem (WaterLogs e Nutrition_MealLogs), só para leitura.
public interface IGlp1Repository
{
    Task<Glp1Profile?> ObterPerfilAsync(string userId);
    Task SalvarPerfilAsync(Glp1Profile perfil);
    Task ApagarPerfilAsync(string userId);

    // Grava em WeightLogs (o mesmo do comando "peso 72kg"): um registro por dia de Brasília, substituído se já houver
    Task RegistrarPesoAsync(string userId, decimal pesoKg, DateTime agoraUtc);

    Task<int> ContarSintomasAsync(string userId);
    Task<int> ContarAlertasAsync(string userId);

    Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc);
    Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc);

    // Apaga perfil, sintomas e alertas do telefone. Não mexe em WaterLogs, WeightLogs nem refeições.
    Task ApagarDadosAsync(string userId);
}
