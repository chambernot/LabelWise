using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Application.Services.Nutrition;
using LabelWise.Domain.Entities.Nutrition;

namespace LabelWise.NutricaoFacil.Tests.Nutrition;

public class NutritionServiceTests
{
    private const string Telefone = "5511999990000";

    private readonly FakeAgente _agente = new();
    private readonly FakeNutritionRepository _repository = new();

    private NutritionService Servico() => new(_agente, _repository);

    private static ParseMealRequestDto Pedido(string texto = "almocei frango") =>
        new(Telefone, texto, null, null, DateTime.UtcNow, null);

    // ================================================================ falha da IA

    [Fact]
    public async Task FalhaDaIA_NaoGravaRefeicao()
    {
        _agente.Analise = MealAnalysisResponseDto.Falha();

        var resultado = await Servico().ProcessMealEntryAsync(Pedido());

        Assert.True(resultado.AnalysisFailed);
        Assert.Empty(_repository.RefeicoesGravadas);
    }

    [Fact]
    public async Task RefeicaoNormal_ContinuaSendoGravada()
    {
        _agente.Analise = new MealAnalysisResponseDto("Almoço", "Frango",
            new List<FoodItemDto> { new("Frango", "120 g", 120, 198, 37, 0, 4, 1) },
            new MacroSummaryDto(198, 37, 0, 4), false, null);

        await Servico().ProcessMealEntryAsync(Pedido());

        var gravada = Assert.Single(_repository.RefeicoesGravadas);
        Assert.Equal(198, gravada.Calories);
    }

    [Fact]
    public async Task AguaCom0Kcal_ContinuaSendoGravada()
    {
        _agente.Analise = new MealAnalysisResponseDto("Lanche", "Chá verde",
            new List<FoodItemDto> { new("Chá verde", "1 xícara", 200, 0, 0, 0, 0, 1) },
            new MacroSummaryDto(0, 0, 0, 0), false, null);

        await Servico().ProcessMealEntryAsync(Pedido("tomei chá verde"));

        Assert.Equal(0, Assert.Single(_repository.RefeicoesGravadas).Calories);
    }

    // ================================================================ sugestões

    [Fact]
    public async Task StatusUsuarioComum_SugestoesSemContextoGlp1()
    {
        await Servico().GetDailyStatusAndSuggestionAsync(Telefone, DateTime.UtcNow.AddHours(-3));

        Assert.Equal(1, _agente.ChamadasSugestoes);
        Assert.Null(_agente.UltimoGlp1);
    }

    [Fact]
    public async Task StatusGlp1_SugestoesComRestricoesAversoesEProteinaQueFalta()
    {
        _repository.Paciente = new PatientDto(Telefone, "b2c_autonomous_user", "", Telefone, MedicalRestrictions: "Lactose", FoodAversions: "Peixe");
        _repository.Refeicoes.Add(new MealLog(Telefone, "Almoço", "Frango", 300, 30, 10, 5, DateTime.UtcNow));

        await Servico().GetDailyStatusAndSuggestionAsync(Telefone, DateTime.UtcNow.AddHours(-3), glp1ProteinTargetG: 86);

        Assert.Equal(new Glp1SuggestionContext("Lactose", "Peixe", 56), _agente.UltimoGlp1);
    }

    // ================================================================ dublês

    private sealed class FakeAgente : INutritionAgentService
    {
        public MealAnalysisResponseDto Analise { get; set; } = MealAnalysisResponseDto.Falha();
        public int ChamadasSugestoes { get; private set; }
        public Glp1SuggestionContext? UltimoGlp1 { get; private set; }

        public Task<List<string>> GenerateProactiveSuggestionsAsync(MacroSummaryDto remainingBalance, string nextMealType, List<string>? pratosJaConsumidos = null, Glp1SuggestionContext? glp1 = null)
        {
            ChamadasSugestoes++;
            UltimoGlp1 = glp1;
            return Task.FromResult(new List<string>());
        }

        public Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request) => Task.FromResult(Analise);
        public Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request, List<ChatMessageLog>? chatHistory) => Task.FromResult(Analise);

        public Task<ExtractDietGoalResponseDto> ExtractDietGoalsFromDocumentAsync(ExtractDietGoalRequestDto request) => throw new NotSupportedException();
        public Task<string> GenerateDailyFeedbackMessageAsync(string patientGoal, int targetCalories, int consumedCalories, decimal targetProtein, decimal consumedProtein, List<string> mealsLogged) => throw new NotSupportedException();
        public Task<string> GenerateSmartSubstitutionAsync(string foodToSubstitute, MacroSummaryDto remainingBalance, string clinicalProtocol, string foodAversions) => throw new NotSupportedException();
        public Task<string> GenerateWeeklyProgressSummaryAsync(string patientName, decimal initialWeight, decimal currentWeight, int targetCalories, double avgConsumedCalories, int avgWaterMl, int streakDays) => throw new NotSupportedException();
        public Task<AutoCalculateGoalDto> CalculateNutritionalGoalsAsync(string userProfileText, string mainGoal) => throw new NotSupportedException();
    }

    private sealed class FakeNutritionRepository : INutritionRepository
    {
        public PatientDto? Paciente { get; set; }
        public List<MealLog> Refeicoes { get; } = new();
        public List<MealLog> RefeicoesGravadas { get; } = new();

        public Task<PatientDto?> ObterPacientePorIdAsync(string patientId) => Task.FromResult(Paciente);
        public Task<List<MealLog>> ObterRefeicoesDoDiaAsync(string userId, DateTime data) => Task.FromResult(Refeicoes.ToList());
        public Task<DailyNutritionGoal> ObterMetaDiariaAsync(string userId, DateTime data) => Task.FromResult<DailyNutritionGoal>(null!);
        public Task InserirMealLogAsync(MealLog mealLog) { RefeicoesGravadas.Add(mealLog); return Task.CompletedTask; }
        public Task<int> CalcularOfensivaStreakAsync(string userId) => Task.FromResult(0);
        public Task<List<ChatMessageLog>> ObterUltimasMensagensAsync(string userId, int limite = 6) => Task.FromResult(new List<ChatMessageLog>());

        public Task<List<string>> ObterTelefonesAtivosAsync() => throw new NotSupportedException();
        public Task SalvarClarificacaoPendenteAsync(MealClarificationContext context) => throw new NotSupportedException();
        public Task<MealClarificationContext?> ObterClarificacaoPendenteAsync(string userId) => throw new NotSupportedException();
        public Task RemoverClarificacaoPendenteAsync(string userId) => throw new NotSupportedException();
        public Task SalvarMetaDiariaAsync(DailyNutritionGoal goal) => throw new NotSupportedException();
        public Task ExcluirMealLogAsync(string id) => throw new NotSupportedException();
        public Task InserirPacienteAsync(PatientDto paciente) => throw new NotSupportedException();
        public Task<List<PatientDto>> ObterPacientesPorProfissionalAsync(string professionalId) => throw new NotSupportedException();
        public Task SalvarMensagemHistoricoAsync(string userId, string role, string content) => throw new NotSupportedException();
    }
}
