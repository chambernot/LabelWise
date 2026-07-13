using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine de classificação de processamento baseado em NOVA.
/// Não depende apenas de quantidade de ingredientes, mas de sinais estruturais.
/// </summary>
public sealed class ProcessingLevelEngine
{
    // Indicadores de NOVA 4 (ultraprocessados)
    private static readonly string[] UltraProcessedIndicators =
    [
        // Aditivos típicos de ultraprocessados
        "corante", "conservante", "estabilizante", "emulsificante", "espessante",
        "aromatizante", "realçador de sabor", "regulador de acidez",
        
        // Aditivos específicos
        "glutamato monossódico", "msg", "e621",
        "aspartame", "sucralose", "acessulfame",
        "tartrazina", "amarelo crepúsculo", "vermelho 40",
        "carragena", "goma xantana", "goma guar",
        "lecitina de soja", "mono e diglicerídeos",
        
        // Gorduras industriais
        "gordura hidrogenada", "gordura vegetal hidrogenada",
        "óleo interesterificado",
        
        // Proteínas isoladas
        "proteína isolada", "isolado proteico",
        "proteína texturizada", "proteína hidrolisada",
        
        // Açúcares processados
        "xarope de milho", "xarope de glicose",
        "maltodextrina", "dextrose",
        
        // Outros indicadores
        "aroma artificial", "sabor artificial",
        "extrato de malte", "extrato de levedura"
    ];

    // Indicadores de NOVA 3 (processados)
    private static readonly string[] ProcessedIndicators =
    [
        "conserva", "enlatado", "defumado",
        "queijo", "pão", "massa",
        "sal", "açúcar", "óleo vegetal",
        "fermento", "vinagre"
    ];

    // Alimentos naturais/minimamente processados
    private static readonly string[] MinimallyProcessedIndicators =
    [
        "farinha de trigo integral", "farinha de aveia",
        "arroz", "feijão", "lentilha", "grão de bico",
        "leite pasteurizado", "iogurte natural",
        "frutas", "vegetais", "legumes",
        "carne", "frango", "peixe",
        "ovo", "ovos"
    ];

    public ProcessingLevel Classify(
        IReadOnlyList<string> ingredients,
        Dictionary<string, string> nutritionalContext)
    {
        if (!ingredients.Any())
            return ProcessingLevel.Unknown;

        var normalizedIngredients = ingredients
            .Select(i => IngredientTextNormalizer.Normalize(i))
            .ToList();

        // Calcular scores
        var ultraProcessedScore = CalculateScore(normalizedIngredients, UltraProcessedIndicators);
        var processedScore = CalculateScore(normalizedIngredients, ProcessedIndicators);
        var minimalScore = CalculateScore(normalizedIngredients, MinimallyProcessedIndicators);

        // Regra 1: Se tem múltiplos indicadores de ultraprocessado, é NOVA 4
        if (ultraProcessedScore >= 2)
            return ProcessingLevel.UltraProcessed;

        // Regra 2: Se tem 1 indicador forte de ultraprocessado, é NOVA 4
        if (HasStrongUltraProcessedIndicator(normalizedIngredients))
            return ProcessingLevel.UltraProcessed;

        // Regra 3: Muitos ingredientes (>5) + aditivos = ultraprocessado
        if (ingredients.Count > 5 && ultraProcessedScore >= 1)
            return ProcessingLevel.UltraProcessed;

        // Regra 4: Se é processado mas não ultraprocessado
        if (processedScore > 0 && ultraProcessedScore == 0)
            return ProcessingLevel.Processed;

        // Regra 5: Poucos ingredientes naturais
        if (ingredients.Count <= 3 && minimalScore > 0 && ultraProcessedScore == 0)
            return ProcessingLevel.MinimallyProcessed;

        // Regra 6: Default baseado em complexidade
        if (ingredients.Count > 10)
            return ProcessingLevel.UltraProcessed;
        
        if (ingredients.Count > 5)
            return ProcessingLevel.Processed;

        return ProcessingLevel.MinimallyProcessed;
    }

    public int CalculateProcessingScore(ProcessingLevel level)
    {
        return level switch
        {
            ProcessingLevel.MinimallyProcessed => 90,
            ProcessingLevel.ProcessedCulinaryIngredients => 60,
            ProcessingLevel.Processed => 40,
            ProcessingLevel.UltraProcessed => 20,
            _ => 50
        };
    }

    public string GetProcessingDescription(ProcessingLevel level)
    {
        return level switch
        {
            ProcessingLevel.MinimallyProcessed => "Alimento natural ou minimamente processado",
            ProcessingLevel.ProcessedCulinaryIngredients => "Ingrediente culinário processado",
            ProcessingLevel.Processed => "Alimento processado",
            ProcessingLevel.UltraProcessed => "Alimento ultraprocessado",
            _ => "Classificação desconhecida"
        };
    }

    public string GetProcessingWarning(ProcessingLevel level)
    {
        return level switch
        {
            ProcessingLevel.UltraProcessed => 
                "Alimentos ultraprocessados devem ser evitados. Preferir alimentos in natura ou minimamente processados.",
            ProcessingLevel.Processed => 
                "Alimento processado. Consumir com moderação, priorizando alimentos naturais.",
            ProcessingLevel.MinimallyProcessed => 
                "Alimento minimamente processado. Boa escolha para uma alimentação saudável.",
            _ => ""
        };
    }

    private static int CalculateScore(IReadOnlyList<string> ingredients, string[] indicators)
    {
        var score = 0;
        
        foreach (var ingredient in ingredients)
        {
            foreach (var indicator in indicators)
            {
                if (ingredient.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                    break; // Contar apenas uma vez por ingrediente
                }
            }
        }

        return score;
    }

    private static bool HasStrongUltraProcessedIndicator(IReadOnlyList<string> ingredients)
    {
        var strongIndicators = new[]
        {
            "gordura hidrogenada",
            "glutamato monossódico",
            "aspartame",
            "proteína isolada",
            "xarope de milho"
        };

        return ingredients.Any(ing =>
            strongIndicators.Any(indicator =>
                ing.Contains(indicator, StringComparison.OrdinalIgnoreCase)));
    }
}
