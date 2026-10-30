using System.Net;
using System.Text;
using System.Text.Json;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelWise.NutricaoFacil.Tests.Nutrition;

// Caminhos de falha da IA: nenhum deles pode inventar refeição (antes viravam 400 ou 350 kcal)
public class NutritionAgentServiceFalhaTests
{
    private static readonly ParseMealRequestDto Pedido = new("5511999990000", "almocei", null, null, DateTime.UtcNow, null);

    private static NutritionAgentService CriarAgente(Func<HttpResponseMessage> resposta)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GeminiApiKey"] = "chave-de-teste", ["Model"] = "modelo-teste" })
            .Build();

        return new NutritionAgentService(new FakeHttpClientFactory(resposta), config, NullLogger<NutritionAgentService>.Instance);
    }

    // Formato da resposta do endpoint compatível com OpenAI
    private static Func<HttpResponseMessage> ConteudoDaIA(string conteudo) => () =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = conteudo } } } }),
                Encoding.UTF8, "application/json")
        };

    private static async Task<MealAnalysisResponseDto> Analisar(Func<HttpResponseMessage> resposta) =>
        await CriarAgente(resposta).ExtractMealDataAsync(Pedido, null);

    private static void AssertFalha(MealAnalysisResponseDto resultado)
    {
        Assert.True(resultado.AnalysisFailed);
        Assert.Equal(0, resultado.TotalMeal.Calories);
        Assert.Equal(0, resultado.TotalMeal.ProteinG);
        Assert.Empty(resultado.Items);
        Assert.False(resultado.RequiresUserClarification);
        Assert.False(resultado.IsAdvice);
    }

    [Fact]
    public async Task ErroHttp_EhFalha()
    {
        AssertFalha(await Analisar(() => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("erro") }));
    }

    [Fact]
    public async Task ExcecaoDeRede_EhFalha()
    {
        AssertFalha(await Analisar(() => throw new HttpRequestException("sem conexão")));
    }

    [Fact]
    public async Task RespostaSemJson_EhFalha_NaoInventa350Kcal()
    {
        AssertFalha(await Analisar(ConteudoDaIA("Desculpe, não consegui identificar a refeição.")));
    }

    [Fact]
    public async Task JsonInvalido_EhFalha()
    {
        AssertFalha(await Analisar(ConteudoDaIA("{ isso não é json }")));
    }

    [Fact]
    public async Task ZeroKcalSemItens_EhFalha_NaoInventa400Kcal()
    {
        AssertFalha(await Analisar(ConteudoDaIA(@"{ ""isAdvice"": false, ""dishName"": ""Almoço"", ""items"": [], ""totalMeal"": { ""calories"": 0 } }")));
    }

    [Fact]
    public async Task ZeroKcalComAlimentoQueTemCaloria_EhFalha()
    {
        AssertFalha(await Analisar(ConteudoDaIA(
            @"{ ""items"": [ { ""foodName"": ""Arroz branco"", ""calories"": 0 } ], ""totalMeal"": { ""calories"": 0 } }")));
    }

    [Theory]
    [InlineData("Água")]
    [InlineData("Café sem açúcar")]
    [InlineData("Chá verde")]
    public async Task ZeroKcalDeAguaCafeOuCha_ContinuaValendo(string bebida)
    {
        var resultado = await Analisar(ConteudoDaIA(
            $@"{{ ""mealType"": ""Lanche"", ""dishName"": ""{bebida}"", ""items"": [ {{ ""foodName"": ""{bebida}"", ""portionDescription"": ""1 xícara"", ""calories"": 0 }} ], ""totalMeal"": {{ ""calories"": 0 }} }}"));

        Assert.False(resultado.AnalysisFailed);
        Assert.Equal(0, resultado.TotalMeal.Calories);
        Assert.Single(resultado.Items);
    }

    [Fact]
    public async Task PedidoDeEsclarecimentoComZeros_NaoEhFalha()
    {
        var resultado = await Analisar(ConteudoDaIA(
            @"{ ""requiresUserClarification"": true, ""clarificationQuestion"": ""Quantas colheres de arroz?"", ""items"": [], ""totalMeal"": { ""calories"": 0 } }"));

        Assert.False(resultado.AnalysisFailed);
        Assert.True(resultado.RequiresUserClarification);
        Assert.Equal(0, resultado.TotalMeal.Calories);
    }

    [Fact]
    public async Task Conselho_NaoEhFalha()
    {
        var resultado = await Analisar(ConteudoDaIA(@"{ ""isAdvice"": true, ""adviceText"": ""Prefira proteína magra."" }"));

        Assert.False(resultado.AnalysisFailed);
        Assert.True(resultado.IsAdvice);
    }

    [Fact]
    public async Task RefeicaoNormal_ContinuaIgual()
    {
        var resultado = await Analisar(ConteudoDaIA(
            @"{ ""mealType"": ""Almoço"", ""dishName"": ""Frango"", ""items"": [ { ""foodName"": ""Frango"", ""calories"": 198, ""proteinG"": 37 } ], ""totalMeal"": { ""calories"": 198, ""proteinG"": 37 } }"));

        Assert.False(resultado.AnalysisFailed);
        Assert.Equal(198, resultado.TotalMeal.Calories);
    }

    // ================================================================ sugestões

    [Fact]
    public async Task SugestoesUsuarioComum_SemJson_ContinuamNoFallbackDeSempre()
    {
        var sugestoes = await CriarAgente(ConteudoDaIA("texto sem json"))
            .GenerateProactiveSuggestionsAsync(new MacroSummaryDto(800, 50, 80, 20), "Jantar");

        Assert.Equal(new[] { "Omelete de 3 ovos", "Frango grelhado e salada", "Shake de whey protein" }, sugestoes);
    }

    [Fact]
    public async Task SugestoesGlp1_NoMaximoDuas()
    {
        var sugestoes = await CriarAgente(ConteudoDaIA(@"{ ""suggestions"": [""Iogurte natural com whey"", ""1 ovo cozido"", ""Queijo cottage""] }"))
            .GenerateProactiveSuggestionsAsync(new MacroSummaryDto(800, 50, 80, 20), "Jantar", null, new Glp1SuggestionContext("Lactose", "Peixe", 40));

        Assert.Equal(new[] { "Iogurte natural com whey", "1 ovo cozido" }, sugestoes);
    }

    [Fact]
    public async Task SugestoesGlp1_ComErro_NaoMostraNada()
    {
        var sugestoes = await CriarAgente(() => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("erro") })
            .GenerateProactiveSuggestionsAsync(new MacroSummaryDto(800, 50, 80, 20), "Jantar", null, new Glp1SuggestionContext(null, null, 40));

        Assert.Empty(sugestoes);
    }

    [Fact]
    public async Task SugestoesGlp1_PromptLevaRestricoesAversoesEProibicoes()
    {
        var factory = new FakeHttpClientFactory(ConteudoDaIA(@"{ ""suggestions"": [] }"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GeminiApiKey"] = "x" }).Build();
        var agente = new NutritionAgentService(factory, config, NullLogger<NutritionAgentService>.Instance);

        await agente.GenerateProactiveSuggestionsAsync(new MacroSummaryDto(0, 0, 0, 0), "Jantar", null, new Glp1SuggestionContext("Lactose", "Peixe", 40));

        // O corpo é JSON (acentos escapados): lê o texto do prompt de volta
        using var json = JsonDocument.Parse(factory.UltimoCorpo!);
        var corpo = json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Lactose", corpo);
        Assert.Contains("Peixe", corpo);
        Assert.Contains("NO MÁXIMO 2", corpo);
        Assert.Contains("NUNCA sugira pular refeição", corpo);
    }

    [Theory]
    [InlineData("Pule o jantar hoje")]
    [InlineData("Faça jejum até amanhã")]
    [InlineData("Tente comer menos à noite")]
    [InlineData("Reduza a porção do almoço")]
    public void FiltroGlp1_DescartaSugestaoDeComerMenos(string sugestao)
    {
        Assert.Empty(NutritionAgentService.FiltrarSugestoesGlp1(new[] { sugestao }));
    }

    [Fact]
    public void FiltroGlp1_MantemSugestaoValida()
    {
        Assert.Equal(new[] { "1 iogurte natural com 1 colher de whey" },
            NutritionAgentService.FiltrarSugestoesGlp1(new[] { "1 iogurte natural com 1 colher de whey", "  " }));
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpResponseMessage> _resposta;
        public string? UltimoCorpo { get; private set; }

        public FakeHttpClientFactory(Func<HttpResponseMessage> resposta) => _resposta = resposta;

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler : HttpMessageHandler
        {
            private readonly FakeHttpClientFactory _factory;
            public Handler(FakeHttpClientFactory factory) => _factory = factory;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _factory.UltimoCorpo = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
                return _factory._resposta();
            }
        }
    }
}
