using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.BackgroundServices
{
    public class ProactiveNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProactiveNotificationBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public ProactiveNotificationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ProactiveNotificationBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 [BackgroundService] Serviço de Notificações Proativas iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessarEnviosProativosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [BackgroundService] Erro ao executar o ciclo proativo.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessarEnviosProativosAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var nutritionRepo = scope.ServiceProvider.GetRequiredService<INutritionRepository>();
            var whatsAppSender = scope.ServiceProvider.GetRequiredService<IWhatsAppSenderService>();

            var horaBrasilia = DateTime.UtcNow.AddHours(-3).Hour;
            var dataHojeBr = DateTime.UtcNow.AddHours(-3).Date;

            string? mealTimeTrigger = horaBrasilia switch
            {
                8 => "café da manhã",
                12 => "almoço",
                19 => "jantar",
                _ => null
            };

            var telefones = await nutritionRepo.ObterTelefonesAtivosAsync();
            if (telefones == null || !telefones.Any()) return;

            if (mealTimeTrigger != null)
            {
                _logger.LogInformation("⏰ [BackgroundService] A disparar lembretes de {Meal} para {Count} pacientes.", mealTimeTrigger, telefones.Count());

                foreach (var telefone in telefones)
                {
                    try
                    {
                        await whatsAppSender.SendTemplateReminderAsync(telefone, "Paciente", mealTimeTrigger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao enviar lembrete proativo para {Phone}", telefone);
                    }
                }
            }

            if (horaBrasilia == 21)
            {
                _logger.LogInformation("🌙 [BackgroundService] A disparar resumos de fim de dia para {Count} pacientes.", telefones.Count());

                foreach (var phone in telefones)
                {
                    try
                    {
                        var goal = await nutritionRepo.ObterMetaDiariaAsync(phone, dataHojeBr);
                        var logs = await nutritionRepo.ObterRefeicoesDoDiaAsync(phone, dataHojeBr);

                        int targetCalories = goal?.TargetCalories ?? 2000;
                        int consumedCal = logs.Sum(x => x.Calories);
                        int diff = targetCalories - consumedCal;

                        string statusMsg = diff switch
                        {
                            > 200 => $"Faltaram {diff} kcal para a sua meta hoje.",
                            < -200 => $"Ultrapassou a meta em {Math.Abs(diff)} kcal.",
                            _ => "Parabéns, atingiu a meta calórica com precisão!"
                        };

                        string mensagemFinal = $"🌙 *Resumo do seu dia*\n\n" +
                                               $"🔥 *Meta:* {targetCalories} kcal | *Consumido:* {consumedCal} kcal\n" +
                                               $"status: _{statusMsg}_\n\nAmanhã seguimos juntos no foco! 🥗💪";

                        await whatsAppSender.SendTextMessageAsync(phone, mensagemFinal);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao enviar feedback diário para {Phone}", phone);
                    }
                }
            }
        }
    }
}