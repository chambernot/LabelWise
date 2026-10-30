using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Services.Glp1;
using LabelWise.Application.Services.Nutrition;

namespace LabelWise.NutricaoFacil.Tests.Nutrition;

public class WhatsAppMealResponseFormatterTests
{
    private static readonly MealAnalysisResponseDto Refeicao = new(
        "Almoço",
        "Frango com arroz",
        new List<FoodItemDto>
        {
            new("Peito de frango", "120 g", 120, 198, 37.2m, 0, 4.3m, 1),
            new("Arroz branco", "4 colheres", 100, 128, 2.5m, 28.1m, 0.2m, 1)
        },
        new MacroSummaryDto(326, 39.7m, 28.1m, 4.5m),
        false,
        null);

    private static DailyStatusResponseDto Status(int consumido, int meta, int streak = 0, List<string>? sugestoes = null) => new(
        "5511999990000",
        DateTime.UtcNow.Date,
        new MacroSummaryDto(meta, 150, 200, 60),
        new MacroSummaryDto(consumido, 60, 90, 20),
        new MacroSummaryDto(Math.Max(0, meta - consumido), 90, 110, 40),
        sugestoes ?? new List<string> { "Omelete de 3 ovos", "Frango grelhado e salada", "Shake de whey protein" },
        streak);

    // ================================================================ usuário comum: idêntico a antes

    public static IEnumerable<object?[]> CasosUsuarioComum()
    {
        yield return new object?[] { Refeicao, Status(1200, 2000) };                          // faltam > 100 kcal, com sugestões
        yield return new object?[] { Refeicao, Status(1950, 2000) };                          // faltam <= 100: sem sugestões
        yield return new object?[] { Refeicao, Status(2300, 2000, streak: 5) };               // meta ultrapassada, com ofensiva
        yield return new object?[] { Refeicao, Status(500, 2000, sugestoes: new List<string>()) };
        yield return new object?[] { Refeicao, null };                                        // sem status do dia
        yield return new object?[] { Refeicao with { Items = new List<FoodItemDto>() }, Status(800, 2000) };
        yield return new object?[] { Refeicao with { DishName = "" }, Status(800, 2000) };
        yield return new object?[] { Refeicao with { IsAdvice = true, AdviceText = "Prefira proteína magra." }, null };
        yield return new object?[] { Refeicao with { IsAdvice = true, AdviceText = null }, null };
        yield return new object?[] { Refeicao with { RequiresUserClarification = true, ClarificationQuestion = "Quantas colheres de arroz?" }, null };
        yield return new object?[] { Refeicao with { ClarificationQuestion = "Nossos serviços de IA estão instáveis." }, null };
    }

    [Theory]
    [MemberData(nameof(CasosUsuarioComum))]
    public void UsuarioComum_SaidaIdenticaAoFormatoAntigo(MealAnalysisResponseDto refeicao, DailyStatusResponseDto? status)
    {
        Assert.Equal(FormatoAntigo(refeicao, status), WhatsAppMealResponseFormatter.FormatarRefeicao(refeicao, status));
    }

    [Fact]
    public void UsuarioComum_ContinuaMostrandoFaltamKcal()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(1200, 2000));
        Assert.Contains("_Faltam 800 kcal_", texto);
        Assert.DoesNotContain("Proteína hoje", texto);
    }

    // ================================================================ falha da IA (todos os usuários)

    [Fact]
    public void FalhaDaIA_PedeParaMandarDeNovo()
    {
        Assert.Equal("Não consegui analisar essa refeição agora 😕 Pode mandar de novo?",
            WhatsAppMealResponseFormatter.FormatarRefeicao(MealAnalysisResponseDto.Falha(), null));

        Assert.Equal(WhatsAppMealResponseFormatter.RespostaFalhaAnalise,
            WhatsAppMealResponseFormatter.FormatarRefeicao(MealAnalysisResponseDto.Falha(), null, Glp1(proteina: 10)));
    }

    // ================================================================ Modo GLP-1

    // 15h e 20h de Brasília (UTC-3)
    private static readonly DateTime Tarde = new(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Noite = new(2026, 10, 29, 23, 0, 0, DateTimeKind.Utc);

    private static Glp1DayContext Glp1(decimal proteina, int calorias = 900, int agua = 1500, DateTime? agora = null, int refeicoes = 2) =>
        new(ProteinTargetG: 86, CalorieFloorKcal: 1200, WaterTargetMl: 2520,
            ProteinTodayG: proteina, CaloriesTodayKcal: calorias, MealsToday: refeicoes, WaterTodayMl: agua, NowUtc: agora ?? Tarde);

    [Fact]
    public void Glp1_ProteinaAtrasada_MostraQuantoFalta()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(900, 2000), Glp1(proteina: 39.7m));

        Assert.StartsWith("✅ *Refeição registrada:* Frango com arroz", texto);
        Assert.Contains("🔥 *Calorias Totais:* 326 kcal", texto); // dados da refeição continuam
        Assert.Contains("🥩 *Proteína hoje:* 40 g de 86 g (faltam 46 g)", texto);
        Assert.Contains("💧 *Água:* 1,5 L de 2,5 L", texto);
        Assert.DoesNotContain("Faltam", texto);
        Assert.DoesNotContain("kcal_", texto);
        Assert.DoesNotContain("SEU RESUMO DE HOJE", texto);
        Assert.DoesNotContain("OFENSIVA", texto);
    }

    [Fact]
    public void Glp1_ProteinaEmDia_MostraMetaBatida()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(1400, 2000), Glp1(proteina: 90, calorias: 1400));

        Assert.Contains("🥩 *Proteína hoje:* 90 g de 86 g (meta batida ✅)", texto);
        Assert.DoesNotContain("faltam", texto);
        Assert.DoesNotContain(WhatsAppMealResponseFormatter.AvisoIngestaoBaixa, texto);
    }

    [Fact]
    public void Glp1_AbaixoDoPisoDepoisDas18h_SugereLanche()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(700, 2000), Glp1(proteina: 30, calorias: 700, agora: Noite));
        Assert.Contains("Hoje você comeu pouco. Que tal um lanche pequeno com proteína?", texto);
    }

    [Fact]
    public void Glp1_AbaixoDoPisoAntesDas18h_NaoAvisa()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(700, 2000), Glp1(proteina: 30, calorias: 700, agora: Tarde));
        Assert.DoesNotContain(WhatsAppMealResponseFormatter.AvisoIngestaoBaixa, texto);
    }

    [Fact]
    public void Glp1_AcimaDoPisoDepoisDas18h_NaoAvisa()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, Status(1300, 2000), Glp1(proteina: 60, calorias: 1300, agora: Noite));
        Assert.DoesNotContain(WhatsAppMealResponseFormatter.AvisoIngestaoBaixa, texto);
    }

    [Fact]
    public void Glp1_MostraNoMaximoDuasSugestoes()
    {
        var status = Status(900, 2000, sugestoes: new List<string> { "Iogurte natural com whey", "1 ovo cozido", "Terceira sugestão" });

        var texto = WhatsAppMealResponseFormatter.FormatarRefeicao(Refeicao, status, Glp1(proteina: 40));

        Assert.Contains("• Iogurte natural com whey", texto);
        Assert.Contains("• 1 ovo cozido", texto);
        Assert.DoesNotContain("Terceira sugestão", texto);
    }

    [Fact]
    public void Glp1_PedidoDeEsclarecimento_IgualAoDeSempre()
    {
        var pergunta = Refeicao with { RequiresUserClarification = true, ClarificationQuestion = "Quantas colheres de arroz?" };
        Assert.Equal(FormatoAntigo(pergunta, null), WhatsAppMealResponseFormatter.FormatarRefeicao(pergunta, null, Glp1(proteina: 40)));
    }

    [Fact]
    public void ResumoGlp1_MostraProteinaAguaCaloriasComPisoERefeicoes()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarResumoGlp1(Glp1(proteina: 45, calorias: 1050, agua: 1500, refeicoes: 3));

        Assert.Contains("SEU DIA NO MODO GLP-1", texto);
        Assert.Contains("(29/10)", texto);
        Assert.Contains("🥩 *Proteína:* 45 de 86 g (faltam 41 g)", texto);
        Assert.Contains("💧 *Água:* 1,5 de 2,5 L", texto);
        Assert.Contains("🔥 *Calorias:* 1.050 kcal (mínimo de 1.200 kcal)", texto);
        Assert.Contains("🍽️ *Refeições registradas hoje:* 3", texto);
        Assert.DoesNotContain("Carboidratos", texto);
        Assert.DoesNotContain("ACUMULADO DO MÊS", texto);
    }

    [Fact]
    public void ResumoGlp1_AbaixoDoPisoDepoisDas18h_Avisa()
    {
        var texto = WhatsAppMealResponseFormatter.FormatarResumoGlp1(Glp1(proteina: 30, calorias: 600, agora: Noite));
        Assert.Contains(WhatsAppMealResponseFormatter.AvisoIngestaoBaixa, texto);
    }

    // ================================================================ referência: método antigo do WhatsAppController, sem alterações

    private static string FormatoAntigo(MealAnalysisResponseDto aiResult, DailyStatusResponseDto? statusDoDia)
    {
        if (aiResult.ClarificationQuestion != null && aiResult.ClarificationQuestion.Contains("instáveis", StringComparison.OrdinalIgnoreCase))
            return "⚠️ *Ops! Nossos serviços estão instáveis no momento.*\nPor favor, tente novamente em instantes.";

        if (aiResult.IsAdvice)
            return $"💡 *Conselho do Nutri:* \n\n{(!string.IsNullOrWhiteSpace(aiResult.AdviceText) ? aiResult.AdviceText : "Como posso ajudar?")}";

        if (aiResult.RequiresUserClarification)
            return $"🤔 *Só mais um detalhe:*\n{aiResult.ClarificationQuestion}";

        var prato = !string.IsNullOrWhiteSpace(aiResult.DishName) ? aiResult.DishName : aiResult.MealType;
        var calorias = aiResult.TotalMeal?.Calories ?? 0;
        var proteina = aiResult.TotalMeal?.ProteinG ?? 0;
        var carbo = aiResult.TotalMeal?.CarbsG ?? 0;
        var gordura = aiResult.TotalMeal?.FatG ?? 0;

        var msg = $"✅ *Refeição registrada:* {prato}\n\n";

        if (aiResult.Items != null && aiResult.Items.Any())
        {
            msg += "📋 *Itens identificados:*\n";
            foreach (var item in aiResult.Items)
            {
                msg += $"• {item.FoodName} ({item.PortionDescription}) — *{item.Calories} kcal*\n";
            }
            msg += "\n";
        }

        msg += $"🔥 *Calorias Totais:* {calorias} kcal\n" +
               $"🥩 *Proteínas:* {proteina}g\n" +
               $"🍞 *Carboidratos:* {carbo}g\n" +
               $"🥑 *Gorduras:* {gordura}g\n\n";

        if (statusDoDia != null)
        {
            if (statusDoDia.StreakDays > 0) msg += $"🔥 *OFENSIVA:* {statusDoDia.StreakDays} dia(s) seguidos no foco! 🚀\n\n";
            msg += $"📊 *SEU RESUMO DE HOJE*\n• *Calorias:* {statusDoDia.Consumed.Calories} / {statusDoDia.Target.Calories} kcal\n";
            var faltamCal = statusDoDia.Remaining.Calories;
            msg += faltamCal <= 0 ? $"_⚠️ Você atingiu ou ultrapassou sua meta!_\n" : $"_Faltam {faltamCal} kcal_\n";
            msg += $"• *Proteínas:* {statusDoDia.Consumed.ProteinG:F0}g / {statusDoDia.Target.ProteinG:F0}g\n" +
                   $"• *Carboidratos:* {statusDoDia.Consumed.CarbsG:F0}g / {statusDoDia.Target.CarbsG:F0}g\n" +
                   $"• *Gorduras:* {statusDoDia.Consumed.FatG:F0}g / {statusDoDia.Target.FatG:F0}g\n";

            if (faltamCal > 100 && statusDoDia.Suggestions != null && statusDoDia.Suggestions.Any())
            {
                msg += "\n💡 *SUGESTÕES PARA A PRÓXIMA REFEIÇÃO:*\n";
                foreach (var sugestao in statusDoDia.Suggestions) msg += $"• {sugestao}\n";
            }
        }
        return msg;
    }
}
