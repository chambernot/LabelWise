using LabelWise.Domain.Entities.Nutrition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.BackgroundServices
{
    public class PatientAdherenceBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PatientAdherenceBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Roda a cada 6 horas

        public PatientAdherenceBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PatientAdherenceBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧠 [Radar Clínico] Motor de Adesão iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessarAdesaoPacientesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao calcular adesão dos pacientes.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessarAdesaoPacientesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

            var patientsCol = database.GetCollection<BsonDocument>("Nutrition_Patients");
            var logsCol = database.GetCollection<MealLog>("Nutrition_MealLogs");
            var goalsCol = database.GetCollection<DailyNutritionGoal>("DailyGoals");

            var patients = await patientsCol.Find(new BsonDocument()).ToListAsync();
            var dataHoje = DateTime.UtcNow.Date;
            var dataLimite = dataHoje.AddDays(-7); // Analisa os últimos 7 dias

            foreach (var patient in patients)
            {
                var phone = patient["_id"].AsString;

                var logs = await logsCol.Find(x => x.UserId == phone && x.LoggedAt >= dataLimite).ToListAsync();
                var goal = await goalsCol.Find(x => x.UserId == phone).SortByDescending(x => x.TargetDate).FirstOrDefaultAsync();
                int targetCalories = goal?.TargetCalories ?? 2000;

                string novoStatus = "Em Dia"; // Padrão 🟢

                if (!logs.Any())
                {
                    novoStatus = "Inativo"; // 🔴 Sem registros
                }
                else
                {
                    var ultimaRefeicao = logs.Max(x => x.LoggedAt);
                    var diasSemRegistro = (DateTime.UtcNow - ultimaRefeicao).TotalDays;

                    if (diasSemRegistro >= 4)
                    {
                        novoStatus = "Inativo"; // 🔴 Não registra há 4+ dias
                    }
                    else if (diasSemRegistro >= 2)
                    {
                        novoStatus = "Atenção"; // 🟡 Não registra há 2+ dias
                    }
                    else
                    {
                        var diasRegistrados = logs.GroupBy(x => x.LoggedAt.Date);
                        int diasAbaixoDaMeta = 0;

                        foreach (var dia in diasRegistrados)
                        {
                            var caloriasDoDia = dia.Sum(x => x.Calories);
                            if (caloriasDoDia < (targetCalories * 0.7))
                            {
                                diasAbaixoDaMeta++;
                            }
                        }

                        if (diasAbaixoDaMeta > (diasRegistrados.Count() / 2.0))
                        {
                            novoStatus = "Atenção"; // 🟡 Furos frequentes na dieta
                        }
                    }
                }

                var update = Builders<BsonDocument>.Update
                    .Set("ClinicalStatus", novoStatus)
                    .Set("LastAuditDate", DateTime.UtcNow);

                await patientsCol.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", phone), update);
            }

            _logger.LogInformation("✅ [Radar Clínico] Auditoria concluída. {Count} pacientes analisados.", patients.Count);
        }
    }
}