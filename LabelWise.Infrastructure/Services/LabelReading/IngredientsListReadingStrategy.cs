using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LabelWise.Application.Parsing;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Estratégia para ler e estruturar lista de ingredientes.
    /// 
    /// OBJETIVO:
    /// Extrair lista de ingredientes de texto OCR bruto.
    /// 
    /// PROCESSO:
    /// 1. Usar IngredientAllergenParser para parsing
    /// 2. Extrair lista de ingredientes
    /// 3. Limpar e normalizar ingredientes
    /// 4. Validar qualidade da extração
    /// 5. Retornar JSON com lista de ingredientes
    /// </summary>
    public class IngredientsListReadingStrategy : ICaptureReadingStrategy
    {
        private readonly IIngredientAllergenParser _parser;
        private readonly ILogger _logger;

        public IngredientsListReadingStrategy(
            IIngredientAllergenParser parser,
            ILogger logger)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
        {
            _logger.LogDebug("🧪 Iniciando parsing de lista de ingredientes...");

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

                if (parseResult.Ingredients == null || !parseResult.Ingredients.Any())
                {
                    result.ErrorMessage = "Nenhum ingrediente encontrado no texto";
                    result.Confidence = Math.Min(ocrConfidence, 0.3);
                    return result;
                }

                // Filtrar ingredientes válidos
                var validIngredients = parseResult.Ingredients
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .Where(i => i.Length >= 2) // Pelo menos 2 caracteres
                    .Where(i => !IsNumericOnly(i))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!validIngredients.Any())
                {
                    result.ErrorMessage = "Nenhum ingrediente válido após filtragem";
                    result.Confidence = Math.Min(ocrConfidence, 0.4);
                    return result;
                }

                // Serializar para JSON
                result.StructuredData = JsonSerializer.Serialize(validIngredients);
                result.Success = true;

                // Ajustar confiança baseado na confiança do parser
                var parserConfidenceScore = parseResult.ParsingConfidence switch
                {
                    LabelWise.Domain.Enums.ConfidenceLevel.High => 1.0,
                    LabelWise.Domain.Enums.ConfidenceLevel.Medium => 0.7,
                    LabelWise.Domain.Enums.ConfidenceLevel.Low => 0.4,
                    _ => 0.5
                };

                result.Confidence = (ocrConfidence + parserConfidenceScore) / 2.0;

                result.Metadata["TotalIngredients"] = validIngredients.Count.ToString();
                result.Metadata["ParserConfidence"] = parseResult.ParsingConfidence.ToString();
                result.Metadata["ValidationWarnings"] = parseResult.ValidationWarnings.Count.ToString();

                _logger.LogDebug("   ✅ Parsing concluído: {Count} ingredientes extraídos",
                    validIngredients.Count);

                if (parseResult.ValidationWarnings.Any())
                {
                    _logger.LogDebug("   ⚠️ Warnings: {Warnings}",
                        string.Join(", ", parseResult.ValidationWarnings));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao fazer parsing da lista de ingredientes");
                result.ErrorMessage = $"Erro no parsing: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        private bool IsNumericOnly(string text)
        {
            return text.All(c => char.IsDigit(c) || c == '.' || c == ',');
        }
    }
}
