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

            // 1. Busca metas do dia (faz fallback automático no repositório se não houver meta específica para a data)
            var dailyGoal = await _repository.ObterMetaDiariaAsync(userId, targetDate);
            var target = dailyGoal != null
                ? new MacroSummaryDto(dailyGoal.TargetCalories, dailyGoal.TargetProteinG, dailyGoal.TargetCarbsG, dailyGoal.TargetFatG)
                : new MacroSummaryDto(2000, 150, 200, 60);

            // 2. Busca histórico de refeições do dia no MongoDB
            var logs = await _repository.ObterRefeicoesDoDiaAsync(userId, targetDate);

            // 3. Soma consumo atual
            int consumedCalories = logs.Sum(x => x.Calories);
            decimal consumedProtein = logs.Sum(x => x.ProteinG);
            decimal consumedCarbs = logs.Sum(x => x.CarbsG);
            decimal consumedFat = logs.Sum(x => x.FatG);

            var consumed = new MacroSummaryDto(consumedCalories, consumedProtein, consumedCarbs, consumedFat);

            // 4. Calcula saldo restante
            var remaining = new MacroSummaryDto(
                Math.Max(0, target.Calories - consumed.Calories),
                Math.Max(0, target.ProteinG - consumed.ProteinG),
                Math.Max(0, target.CarbsG - consumed.CarbsG),
                Math.Max(0, target.FatG - consumed.FatG)
            );

            // 5. Determina o momento da próxima refeição com base no horário informado
            string nextMealType = date.Hour < 11 ? "Almoço" : (date.Hour < 17 ? "Lanche da Tarde" : "Jantar / Ceia");

            // 6. Extrai o nome dos pratos já consumidos hoje para evitar repetições
            var pratosJaConsumidos = logs
                .Select(x => x.DishName)
                .Where(dish => !string.IsNullOrWhiteSpace(dish))
                .ToList();

            // 7. Solicita à IA sugestões personalizadas levando em conta o histórico do dia
            var suggestions = await _aiAgent.GenerateProactiveSuggestionsAsync(remaining, nextMealType, pratosJaConsumidos);

            return new DailyStatusResponseDto(userId, targetDate, target, consumed, remaining, suggestions);
        }

        public async Task<MealAnalysisResponseDto> ProcessMealEntryAsync(ParseMealRequestDto request)
        {
            // 1. Delega a análise pesada (Visão/Áudio/Texto -> JSON) para o agente de IA
            var aiAnalysis = await _aiAgent.ExtractMealDataAsync(request);

            // 2. Se a IA solicitar clarificação (baixa confiança ou dados incompletos), interrompe o salvamento
            if (aiAnalysis.RequiresUserClarification)
            {
                return aiAnalysis;
            }

            // 3. Define a data/hora local correta do registro
            var logTime = request.LocalTime != default ? request.LocalTime : DateTime.UtcNow;

            // 4. Mapeia para a entidade de domínio MealLog
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

            // 5. Persiste no MongoDB
            await _repository.InserirMealLogAsync(mealLog);

            return aiAnalysis;
        }
    }
}