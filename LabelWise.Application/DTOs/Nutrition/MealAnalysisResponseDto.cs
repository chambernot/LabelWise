using System.Collections.Generic;

namespace LabelWise.Application.DTOs.Nutrition;

// AnalysisFailed = a IA não conseguiu analisar (erro, resposta vazia ou sem dados). Nada é gravado e o usuário é
// convidado a mandar de novo. Antes, esses casos viravam uma refeição inventada de 400 ou 350 kcal.
public record MealAnalysisResponseDto(
    string MealType,
    string DishName,
    List<FoodItemDto> Items,
    MacroSummaryDto TotalMeal,
    bool RequiresUserClarification,
    string? ClarificationQuestion,
    bool IsAdvice = false,
    string? AdviceText = null,
    bool AnalysisFailed = false
)
{
    public static MealAnalysisResponseDto Falha() =>
        new("Refeição", "Refeição", new List<FoodItemDto>(), new MacroSummaryDto(0, 0, 0, 0), false, null, AnalysisFailed: true);
}

public record FoodItemDto(
    string FoodName,
    string PortionDescription,
    decimal EstimatedWeightG,
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal ConfidenceScore
);

public record MacroSummaryDto(
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG
);

// Sugestões do Modo GLP-1: restrições e aversões do paciente e quanto falta de proteína no dia
public record Glp1SuggestionContext(string? Restricoes, string? Aversoes, decimal ProteinaRestanteG);
