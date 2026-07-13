using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Estratégia de parsing nutricional a partir de um resultado do Azure Document Intelligence.
/// </summary>
internal interface INutritionParser
{
    /// <summary>Nome identificador da estratégia (para logging).</summary>
    string Name { get; }

    /// <summary>
    /// Extrai dados nutricionais do resultado bruto do Document Intelligence.
    /// Retorna null se a estratégia não for aplicável ao resultado recebido.
    /// </summary>
    DocumentIntelligenceNutritionResult? Parse(AnalyzeResult result);

    /// <summary>
    /// Calcula um score de confiança (0–100) baseado na completude e consistência
    /// do resultado já extraído.
    /// </summary>
    int GetConfidenceScore(DocumentIntelligenceNutritionResult result);
}
