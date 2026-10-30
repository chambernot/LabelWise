using LabelWise.Application.Services.Glp1;
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
    Task InserirSintomasAsync(IEnumerable<Glp1SymptomLog> sintomas);
    Task<List<Glp1SymptomLog>> ObterSintomasDesdeAsync(string userId, DateTime desdeUtc);
    Task InserirAlertaAsync(Glp1Alert alerta);

    // Restrições/alergias e aversões do onboarding (Nutrition_Patients), para filtrar as dicas
    Task<(string? Restricoes, string? Aversoes)> ObterRestricoesEAversoesAsync(string userId);

    // ---------- lembretes
    Task<List<Glp1Profile>> ListarPerfisAtivosAsync();
    Task<Glp1Alert?> ObterUltimoAlertaAsync(string userId, Glp1AlertType tipo);
    Task<bool> LembreteJaEnviadoAsync(string userId, string tipo, string diaBr);

    // Reserva o envio do dia (telefone + tipo + dia). false = já havia um envio desse tipo hoje.
    Task<bool> RegistrarEnvioLembreteAsync(string userId, string tipo, string diaBr, DateTime agoraUtc);

    // Última mensagem recebida da pessoa (UserActivity.LastInboundAt), para decidir entre texto livre e template
    Task<DateTime?> ObterUltimaMensagemRecebidaAsync(string userId);

    // ---------- portal da nutricionista
    Task<List<Glp1Profile>> ObterPerfisAsync(IEnumerable<string> userIds);
    Task<HashSet<string>> ObterTelefonesComAlertaNaoVistoAsync(IEnumerable<string> userIds);

    // Alertas criados desde a data, mais qualquer alerta ainda não visto
    Task<List<Glp1Alert>> ObterAlertasParaPortalAsync(string userId, DateTime desdeUtc);
    Task<bool> MarcarAlertaVistoAsync(string userId, string alertId, DateTime agoraUtc);

    Task<List<Glp1MealEntry>> ObterRefeicoesDesdeAsync(string userId, DateTime desdeUtc);
    Task<List<Glp1WaterEntry>> ObterAguaDesdeAsync(string userId, DateTime desdeUtc);
    Task<List<Glp1WeightEntry>> ObterPesosDesdeAsync(string userId, DateTime desdeUtc);

    Task<int> ContarAlertasAsync(string userId);

    Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc);

    // Proteína, calorias e quantidade de refeições do dia, sem treinos
    Task<Glp1DayIntake> ObterConsumoDoDiaAsync(string userId, DateTime inicioDiaUtc);
    Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc);

    // Apaga perfil, sintomas e alertas do telefone. Não mexe em WaterLogs, WeightLogs nem refeições.
    Task ApagarDadosAsync(string userId);
}
