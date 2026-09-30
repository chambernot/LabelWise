using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Nutrition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LabelWise.Application.Services.Nutrition
{
    public class NutritionService : INutritionService
    {
        private readonly INutritionAgentService _aiAgent;
        private readonly INutritionRepository _repository;

        public NutritionService(
            INutritionAgentService aiAgent,
            INutritionRepository repository)
        {
            _aiAgent = aiAgent ?? throw new ArgumentNullException(nameof(aiAgent));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<List<MealLog>> GetDailyMealsAsync(string userId, DateTime date)
        {
            return await _repository.ObterRefeicoesDoDiaAsync(userId, date.Date);
        }

        public async Task DeleteMealAsync(string mealId)
        {
            await _repository.ExcluirMealLogAsync(mealId);
        }

        public async Task SetUserGoalAsync(SetNutritionGoalDto request)
        {
            var goal = new DailyNutritionGoal(
                request.UserId,
                request.Date,
                request.TargetCalories,
                request.TargetProteinG,
                request.TargetCarbsG,
                request.TargetFatG
            );

            await _repository.SalvarMetaDiariaAsync(goal);
        }

        public async Task<DailyStatusResponseDto> GetDailyStatusAndSuggestionAsync(string userId, DateTime date)
        {
            var targetDate = date.Date;

            var dailyGoal = await _repository.ObterMetaDiariaAsync(userId, targetDate);
            var target = dailyGoal != null
                ? new MacroSummaryDto(dailyGoal.TargetCalories, dailyGoal.TargetProteinG, dailyGoal.TargetCarbsG, dailyGoal.TargetFatG)
                : new MacroSummaryDto(2000, 150, 200, 60);

            var logs = await _repository.ObterRefeicoesDoDiaAsync(userId, targetDate);

            int consumedCalories = logs.Sum(x => x.Calories);
            decimal consumedProtein = logs.Sum(x => x.ProteinG);
            decimal consumedCarbs = logs.Sum(x => x.CarbsG);
            decimal consumedFat = logs.Sum(x => x.FatG);

            var consumed = new MacroSummaryDto(consumedCalories, consumedProtein, consumedCarbs, consumedFat);

            var remaining = new MacroSummaryDto(
                Math.Max(0, target.Calories - consumed.Calories),
                Math.Max(0, target.ProteinG - consumed.ProteinG),
                Math.Max(0, target.CarbsG - consumed.CarbsG),
                Math.Max(0, target.FatG - consumed.FatG)
            );

            string nextMealType = date.Hour < 11 ? "Almoço" : (date.Hour < 17 ? "Lanche da Tarde" : "Jantar / Ceia");

            var pratosJaConsumidos = logs
                .Select(x => x.DishName)
                .Where(dish => !string.IsNullOrWhiteSpace(dish))
                .ToList();

            var suggestions = await _aiAgent.GenerateProactiveSuggestionsAsync(remaining, nextMealType, pratosJaConsumidos);

            int streakDays = await _repository.CalcularOfensivaStreakAsync(userId);

            return new DailyStatusResponseDto(userId, targetDate, target, consumed, remaining, suggestions, streakDays);
        }

        public async Task<MealAnalysisResponseDto> ProcessMealEntryAsync(ParseMealRequestDto request)
        {
            // 🚀 1. BUSCAR O CONTEXTO REAL DO PACIENTE NO BANCO DE DADOS
            var dataHojeBr = DateTime.UtcNow.AddHours(-3);
            var dailyGoal = await _repository.ObterMetaDiariaAsync(request.UserId, dataHojeBr.Date);
            var logs = await _repository.ObterRefeicoesDoDiaAsync(request.UserId, dataHojeBr.Date);

            int targetCal = dailyGoal?.TargetCalories ?? 2000;
            decimal targetProt = dailyGoal?.TargetProteinG ?? 150;
            decimal targetCarb = dailyGoal?.TargetCarbsG ?? 200;
            decimal targetFat = dailyGoal?.TargetFatG ?? 60;

            int consumedCal = logs.Sum(x => x.Calories);
            decimal consumedProt = logs.Sum(x => x.ProteinG);
            decimal consumedCarb = logs.Sum(x => x.CarbsG);
            decimal consumedFat = logs.Sum(x => x.FatG);

            int remainingCal = Math.Max(0, targetCal - consumedCal);

            // Formata o contexto clínico atual para a IA usar
            string contextoPaciente = $"\n\n[CONTEXTO CLÍNICO DO PACIENTE HOJE]: " +
                                      $"- Meta Calórica Diária: {targetCal} kcal (Já consumidas: {consumedCal} kcal | Restam: {remainingCal} kcal)\n" +
                                      $"- Metas de Macros: Proteína {targetProt}g (Consumido: {consumedProt:F0}g), " +
                                      $"Carboidratos {targetCarb}g (Consumido: {consumedCarb:F0}g), " +
                                      $"Gorduras {targetFat}g (Consumido: {consumedFat:F0}g).\n" +
                                      $"- Refeições já registradas hoje: {logs.Count} refeições.";

            // Enriquece o texto de entrada com a realidade do paciente
            var textoOriginal = request.TextInput ?? string.Empty;
            var textoEnriquecido = textoOriginal + contextoPaciente;

            var enrichedRequest = new ParseMealRequestDto(
                request.UserId,
                textoEnriquecido,
                request.Base64Image,
                request.AudioUrl,
                request.LocalTime
            );

            // 2. Delega a análise para o agente de IA com o contexto completo
            var aiAnalysis = await _aiAgent.ExtractMealDataAsync(enrichedRequest);

            // Se for conselho (Modo SOS), retorna direto sem salvar log
            if (aiAnalysis.IsAdvice)
            {
                return aiAnalysis;
            }

            if (aiAnalysis.RequiresUserClarification)
            {
                return aiAnalysis;
            }

            var logTime = request.LocalTime != default ? request.LocalTime : DateTime.UtcNow;

            var mealLog = new MealLog(
                userId: request.UserId,
                mealType: aiAnalysis.MealType ?? "Desconhecido",
                dishName: aiAnalysis.DishName ?? "Refeição",
                calories: aiAnalysis.TotalMeal?.Calories ?? 0,
                proteinG: aiAnalysis.TotalMeal?.ProteinG ?? 0,
                carbsG: aiAnalysis.TotalMeal?.CarbsG ?? 0,
                fatG: aiAnalysis.TotalMeal?.FatG ?? 0,
                loggedAt: logTime
            );

            await _repository.InserirMealLogAsync(mealLog);

            return aiAnalysis;
        }
    }
}