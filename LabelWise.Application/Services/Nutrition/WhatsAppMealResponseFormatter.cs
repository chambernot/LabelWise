using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Services.Glp1;
using System.Globalization;
using System.Text;

namespace LabelWise.Application.Services.Nutrition;

// Textos que o bot do WhatsApp envia depois de um registro de refeição e no comando "resumo" do Modo GLP-1.
// Sem contexto GLP-1, FormatarRefeicao devolve exatamente o mesmo texto de antes (há teste comparando com o formato antigo).
public static class WhatsAppMealResponseFormatter
{
    public const string RespostaFalhaAnalise = "Não consegui analisar essa refeição agora 😕 Pode mandar de novo?";
    public const string AvisoIngestaoBaixa = "Hoje você comeu pouco. Que tal um lanche pequeno com proteína?";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string FormatarRefeicao(MealAnalysisResponseDto aiResult, DailyStatusResponseDto? statusDoDia, Glp1DayContext? glp1 = null)
    {
        if (aiResult.AnalysisFailed)
            return RespostaFalhaAnalise;

        if (aiResult.ClarificationQuestion != null && aiResult.ClarificationQuestion.Contains("instáveis", StringComparison.OrdinalIgnoreCase))
            return "⚠️ *Ops! Nossos serviços estão instáveis no momento.*\nPor favor, tente novamente em instantes.";

        if (aiResult.IsAdvice)
            return $"💡 *Conselho do Nutri:* \n\n{(!string.IsNullOrWhiteSpace(aiResult.AdviceText) ? aiResult.AdviceText : "Como posso ajudar?")}";

        if (aiResult.RequiresUserClarification)
            return $"🤔 *Só mais um detalhe:*\n{aiResult.ClarificationQuestion}";

        var msg = FormatarDadosDaRefeicao(aiResult);

        if (glp1 != null)
        {
            return msg + FormatarDiaGlp1(glp1, statusDoDia?.Suggestions);
        }

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

    private static string FormatarDadosDaRefeicao(MealAnalysisResponseDto aiResult)
    {
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

        return msg;
    }

    // Modo GLP-1: foco em proteína e água; sem "faltam X kcal", sem ofensiva calórica
    private static string FormatarDiaGlp1(Glp1DayContext glp1, List<string>? sugestoes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🥩 *Proteína hoje:* {Gramas(glp1.ProteinTodayG)} g de {glp1.ProteinTargetG} g {TextoFaltaProteina(glp1)}");
        sb.AppendLine($"💧 *Água:* {Litros(glp1.WaterTodayMl)} L de {Litros(glp1.WaterTargetMl)} L");

        if (glp1.IngestaoBaixaNoFimDoDia)
        {
            sb.AppendLine();
            sb.AppendLine(AvisoIngestaoBaixa);
        }

        var ideias = (sugestoes ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Take(2).ToList();
        if (ideias.Any())
        {
            sb.AppendLine();
            sb.AppendLine("💡 *Ideias leves para a próxima refeição:*");
            foreach (var ideia in ideias) sb.AppendLine($"• {ideia}");
        }

        return sb.ToString();
    }

    // "resumo" do Modo GLP-1: proteína, água, calorias com o piso e refeições do dia
    public static string FormatarResumoGlp1(Glp1DayContext glp1)
    {
        var hojeBr = glp1.NowUtc.AddHours(-3);
        var sb = new StringBuilder();

        sb.AppendLine("📊 *SEU DIA NO MODO GLP-1* 📊\n");
        sb.AppendLine($"🗓️ *HOJE ({hojeBr:dd/MM})*");

        sb.AppendLine($"🥩 *Proteína:* {Gramas(glp1.ProteinTodayG)} de {glp1.ProteinTargetG} g {TextoFaltaProteina(glp1)}");
        sb.AppendLine(GerarBarraProgresso(glp1.ProteinTodayG, glp1.ProteinTargetG));

        sb.AppendLine($"💧 *Água:* {Litros(glp1.WaterTodayMl)} de {Litros(glp1.WaterTargetMl)} L");
        sb.AppendLine(GerarBarraProgresso(glp1.WaterTodayMl, glp1.WaterTargetMl));

        sb.AppendLine($"🔥 *Calorias:* {glp1.CaloriesTodayKcal.ToString("N0", PtBr)} kcal (mínimo de {glp1.CalorieFloorKcal.ToString("N0", PtBr)} kcal)");
        sb.AppendLine(GerarBarraProgresso(glp1.CaloriesTodayKcal, glp1.CalorieFloorKcal));

        sb.AppendLine($"🍽️ *Refeições registradas hoje:* {glp1.MealsToday}");

        if (glp1.IngestaoBaixaNoFimDoDia)
        {
            sb.AppendLine();
            sb.AppendLine(AvisoIngestaoBaixa);
        }

        sb.AppendLine("\n💡 *Dica:* Digite *glp1 status* para ver seus dados do Modo GLP-1 ou *remover* para ajustar uma refeição de hoje.");

        return sb.ToString();
    }

    public static string GerarBarraProgresso(decimal atual, decimal meta, int tamanhoBarra = 10)
    {
        if (meta <= 0) meta = 1;
        decimal porcentagem = Math.Min(1.0m, Math.Max(0.0m, atual / meta));
        int preenchidos = (int)Math.Round(porcentagem * tamanhoBarra);
        int vazios = tamanhoBarra - preenchidos;

        var sb = new StringBuilder();
        for (int i = 0; i < preenchidos; i++) sb.Append("🟩");
        for (int i = 0; i < vazios; i++) sb.Append("⬜");

        return $"{sb} {porcentagem * 100:F0}%";
    }

    private static string TextoFaltaProteina(Glp1DayContext glp1) =>
        glp1.ProteinRemainingG > 0 ? $"(faltam {Gramas(glp1.ProteinRemainingG)} g)" : "(meta batida ✅)";

    private static string Gramas(decimal valor) => Math.Round(valor, 0, MidpointRounding.AwayFromZero).ToString("0", PtBr);

    // 1500 ml → "1,5"; 2520 ml → "2,5"; 0 → "0"
    private static string Litros(int ml) => Math.Round(ml / 1000m, 1, MidpointRounding.AwayFromZero).ToString("0.#", PtBr);
}
