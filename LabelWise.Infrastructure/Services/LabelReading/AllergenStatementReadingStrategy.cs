using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LabelWise.Application.Parsing;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Estratégia para ler e estruturar declarações de alérgenos.
    /// 
    /// OBJETIVO:
    /// Extrair declarações de alérgenos de texto OCR bruto.
    /// 
    /// PROCESSO:
    /// 1. Usar IngredientAllergenParser para parsing
    /// 2. Extrair alérgenos confirmados e possíveis
    /// 3. Consolidar em lista única
    /// 4. Validar qualidade da extração
    /// 5. Retornar JSON com lista de alérgenos
    /// </summary>
    public class AllergenStatementReadingStrategy : ICaptureReadingStrategy
    {
        private readonly IIngredientAllergenParser _parser;
        private readonly ILogger _logger;

        public AllergenStatementReadingStrategy(
            IIngredientAllergenParser parser,
            ILogger logger)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
        {
            _logger.LogDebug("⚠️ Iniciando parsing de declaração de alérgenos...");

            var result = new CaptureReadingStrategyResult
            {
                Success = false,
                Confidence = ocrConfidence
            };

            if (string.IsNullOrWhiteSpace(rawOcrText))
            {
                result.ErrorMessage = "Texto OCR vazio";
                return result;
            }

            try
            {
                // Usar o parser existente
                var parseResult = _parser.Parse(rawOcrText);

                // Consolidar alérgenos confirmados e possíveis
                var allergens = new List<string>();

                if (parseResult.ConfirmedAllergens != null && parseResult.ConfirmedAllergens.Any())
                {
                    allergens.AddRange(parseResult.ConfirmedAllergens.Select(a => $"Contém: {a}"));
                }

                if (parseResult.MayContainAllergens != null && parseResult.MayContainAllergens.Any())
                {
                    allergens.AddRange(parseResult.MayContainAllergens.Select(a => $"Pode conter: {a}"));
                }

                // Se não encontrou nada, mas há frases críticas, considerar como informação útil
                if (!allergens.Any() && parseResult.ExtractedPhrases != null && parseResult.ExtractedPhrases.Any())
                {
                    _logger.LogDebug("   → Nenhum alérgeno específico, mas há {Count} frases críticas",
                        parseResult.ExtractedPhrases.Count);

                    // Adicionar frases críticas como informação
                    allergens.AddRange(parseResult.ExtractedPhrases);
                }

                // Se ainda não tem nada, considerar como "sem declaração de alérgenos"
                if (!allergens.Any())
                {
                    _logger.LogDebug("   → Nenhuma declaração de alérgenos encontrada");

                    // Isso não é necessariamente um erro - o produto pode não ter alérgenos
                    allergens.Add("Nenhuma declaração de alérgenos encontrada");
                    result.Confidence = Math.Min(ocrConfidence, 0.6);
                }
                else
                {
                    // Ajustar confiança baseado na confiança do parser
                    var parserConfidenceScore = parseResult.ParsingConfidence switch
                    {
                        LabelWise.Domain.Enums.ConfidenceLevel.High => 1.0,
                        LabelWise.Domain.Enums.ConfidenceLevel.Medium => 0.7,
                        LabelWise.Domain.Enums.ConfidenceLevel.Low => 0.4,
                        _ => 0.5
                    };

                    result.Confidence = (ocrConfidence + parserConfidenceScore) / 2.0;
                }

                // Serializar para JSON
                result.StructuredData = JsonSerializer.Serialize(allergens);
                result.Success = true;

                result.Metadata["TotalAllergens"] = allergens.Count.ToString();
                result.Metadata["ContainsAllergens"] = (parseResult.ConfirmedAllergens?.Count ?? 0).ToString();
                result.Metadata["MayContainAllergens"] = (parseResult.MayContainAllergens?.Count ?? 0).ToString();
                result.Metadata["ExtractedPhrases"] = (parseResult.ExtractedPhrases?.Count ?? 0).ToString();
                result.Metadata["ParserConfidence"] = parseResult.ParsingConfidence.ToString();

                _logger.LogDebug("   ✅ Parsing concluído: {Total} declarações ({Contains} confirmados, {May} possíveis)",
                    allergens.Count,
                    parseResult.ConfirmedAllergens?.Count ?? 0,
                    parseResult.MayContainAllergens?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao fazer parsing da declaração de alérgenos");
                result.ErrorMessage = $"Erro no parsing: {ex.Message}";
                result.Success = false;
            }

            return result;
        }
    }
}
