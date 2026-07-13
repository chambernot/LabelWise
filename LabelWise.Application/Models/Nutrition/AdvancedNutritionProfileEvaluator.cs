using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Avaliador determinístico de perfis nutricionais.
///
/// Regra geral:
///   - valor null  → trata como desconhecido (mensagem específica de "incerto" / "sem dados")
///   - valor existe → confia no valor extraído (sem gate de confiabilidade)
///
/// Ordenação e thresholds alinhados com apps de referência (estilo Yuka),
/// genéricos para qualquer produto.
/// </summary>
public static class AdvancedNutritionProfileEvaluator
{
    public static UserProfileInsightsDto Evaluate(
        EstimatedNutritionProfileDto? nutrition,
        int score,
        string? principalOffender,
        string? category)
    {
        // Mantém todos os valores nullable — nunca usa 0 como default,
        // o que produziria perfis incorretos.
        double? sugar    = nutrition?.EstimatedSugarPer100g;
        double? sodium   = nutrition?.EstimatedSodiumPer100g;
        double? calories = nutrition?.CaloriesPer100g ?? nutrition?.CaloriesPer100ml;
        double? protein  = nutrition?.EstimatedProteinPer100g;
        double? fat      = nutrition?.EstimatedFatPer100g;
        double? fiber    = nutrition?.EstimatedFiberPer100g;

        return new UserProfileInsightsDto
        {
            Diabetic     = EvaluateDiabetic(sugar),
            Hypertension = EvaluateHypertension(sodium),
            WeightLoss   = EvaluateWeightLoss(calories, fat, sugar),
            MuscleGain   = EvaluateMuscleGain(protein),
            Summary      = BuildSummary(calories, sugar, sodium, protein, fat, fiber, nutrition)
        };
    }

    // ── Diabetic ──────────────────────────────────────────────────────────

    private static string EvaluateDiabetic(double? sugar)
    {
        if (sugar is null)
            return "Impacto glicêmico incerto";

        if (sugar >= 20)
            return "Alto teor de açúcar — não recomendado para diabéticos";

        if (sugar > 10)
            return "Impacto glicêmico moderado";

        return "Baixo impacto glicêmico";
    }

    // ── Hypertension ──────────────────────────────────────────────────────

    private static string EvaluateHypertension(double? sodium)
    {
        if (sodium is null)
            return "Sem dados de sódio disponíveis";

        if (sodium < 200)
            return "Baixo teor de sódio";

        if (sodium < 300)
            return "Sódio moderado: atenção ao consumo frequente";

        return "Alto teor de sódio: evitar consumo frequente";
    }

    // ── Weight Loss ───────────────────────────────────────────────────────
    // Prioridade: 1) açúcar  2) calorias  3) gordura (modificador)

    private static string EvaluateWeightLoss(double? calories, double? fat, double? sugar)
    {
        string result;

        if (sugar >= 20)
        {
            result = "Alto teor de açúcar — pode dificultar emagrecimento";
        }
        else if (sugar > 8)
        {
            result = "Pode dificultar emagrecimento devido ao açúcar";
        }
        else if (calories is null)
        {
            return "Dados insuficientes";
        }
        else if (calories < 150)
        {
            result = "Baixa densidade calórica";
        }
        else if (calories < 300)
        {
            result = "Consumo moderado";
        }
        else
        {
            result = "Alta densidade calórica";
        }

        // Modificador de gordura — aplicado depois da decisão principal.
        if (fat > 20)
            result += ", alto teor de gordura";
        else if (fat > 10)
            result += ", atenção ao teor de gordura";

        return result;
    }

    // ── Muscle Gain ───────────────────────────────────────────────────────

    private static string EvaluateMuscleGain(double? protein)
    {
        if (protein is null)
            return "Impacto no ganho muscular incerto";

        if (protein > 15)
            return "Boa fonte de proteína";

        if (protein > 8)
            return "Proteína moderada";

        return "Baixo teor proteico — não ideal para ganho muscular";
    }

    // ── Summary ───────────────────────────────────────────────────────────
    // Frase única em ordem de prioridade. Nenhum gate de confiabilidade.

    private static string BuildSummary(
        double? calories, double? sugar, double? sodium,
        double? protein, double? fat, double? fiber,
        EstimatedNutritionProfileDto? nutrition)
    {
        // Sinal positivo forte: alta proteína + baixo açúcar
        if (protein > 10 && (sugar is null || sugar < 5))
        {
            var summary = "Produto com bom aporte proteico e baixo teor de açúcar";
            if (nutrition?.EstimatedSaturatedFatPer100g > 2)
                summary += ", porém contém gordura saturada moderada";
            return summary + ".";
        }

        // Alta densidade calórica + gordura elevada
        if (fat > 15 && calories > 300)
            return "Produto com alta densidade calórica e teor de gordura elevado.";

        // Açúcar extremo
        if (sugar >= 30)
            return "Produto com teor extremamente elevado de açúcar.";

        if (sugar >= 20)
            return "Produto com alto teor de açúcar.";

        if (sugar > 10)
        {
            var summary = "Produto com alto teor de açúcar: indicado para consumo ocasional";
            if (nutrition?.EstimatedSaturatedFatPer100g > 2)
                summary += ", e contém gordura saturada moderada";
            return summary + ".";
        }

        if (sodium > 500)
            return "Produto com teor de sódio elevado: atenção ao consumo frequente.";

        if (fiber > 5)
            return "Produto rico em fibras, favorável para a saúde digestiva.";

        if (calories is not null && calories < 150)
            return "Produto com baixa densidade calórica.";

        if (protein > 5)
            return "Produto com razoável aporte proteico. Consumo moderado recomendado.";

        if (sodium > 200)
            return "Atenção ao teor de sódio no consumo frequente.";

        return "Perfil nutricional equilibrado para consumo geral.";
    }
}
