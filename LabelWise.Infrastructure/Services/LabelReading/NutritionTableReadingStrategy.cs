using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.LabelReading;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Estratégia para ler e estruturar tabelas nutricionais.
    /// 
    /// OBJETIVO:
    /// Extrair informações nutricionais estruturadas de texto OCR bruto.
    /// 
    /// PROCESSO:
    /// 1. Identificar seção da tabela nutricional
    /// 2. Extrair tamanho da porção
    /// 3. Extrair valores nutricionais (calorias, carboidratos, proteínas, etc.)
    /// 4. Validar integridade dos dados
    /// 5. Retornar JSON estruturado
    /// </summary>
    public class NutritionTableReadingStrategy : ICaptureReadingStrategy
    {
        private readonly ILogger _logger;

        // Padrões de regex para extração
        private static readonly Regex ServingSizePattern = new(
            @"por[çã]+o[:\s]+(\d+\s*(?:g|ml|mg|unidade[s]?|fatia[s]?|colher[es]?))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CaloriesPattern = new(
            @"(?:valor\s+energ[eé]tico|calorias?|kcal)[:\s]+(\d+(?:[.,]\d+)?)\s*(?:kcal)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CarbohydratesPattern = new(
            @"carboidratos?[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProteinsPattern = new(
            @"prote[íi]nas?[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TotalFatPattern = new(
            @"gorduras?\s+totais?[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SaturatedFatPattern = new(
            @"gorduras?\s+saturadas?[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TransFatPattern = new(
            @"gorduras?\s+trans[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FiberPattern = new(
            @"fibras?\s+alimentar[es]*[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SodiumPattern = new(
            @"s[óo]dio[:\s]+(\d+(?:[.,]\d+)?)\s*mg",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SugarsPattern = new(
            @"a[çc][úu]cares?[:\s]+(\d+(?:[.,]\d+)?)\s*g",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public NutritionTableReadingStrategy(
            LabelWise.Application.Parsing.IIngredientAllergenParser parser,
            ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
        {
            _logger.LogDebug("📊 Iniciando parsing de tabela nutricional...");

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
                var nutritionInfo = ExtractNutritionInfo(rawOcrText);

                if (nutritionInfo == null || !HasMinimumNutritionData(nutritionInfo))
                {
                    result.ErrorMessage = "Não foi possível extrair informações nutricionais mínimas";
                    result.Confidence = Math.Min(ocrConfidence, 0.3);
                    return result;
                }

                // Serializar para JSON
                result.StructuredData = JsonSerializer.Serialize(nutritionInfo);
                result.Success = true;

                // Ajustar confiança baseado na completude dos dados
                var completenessScore = CalculateCompletenessScore(nutritionInfo);
                result.Confidence = (ocrConfidence + completenessScore) / 2.0;

                result.Metadata["ExtractedFields"] = CountExtractedFields(nutritionInfo).ToString();
                result.Metadata["CompletenessScore"] = completenessScore.ToString("F2");

                _logger.LogDebug("   ✅ Parsing concluído: {Fields} campos extraídos, completude {Score:P2}",
                    CountExtractedFields(nutritionInfo), completenessScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao fazer parsing da tabela nutricional");
                result.ErrorMessage = $"Erro no parsing: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        private NutritionalInformationDto ExtractNutritionInfo(string text)
        {
            var info = new NutritionalInformationDto();

            // Normalizar texto
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            // Extrair tamanho da porção
            var servingSizeMatch = ServingSizePattern.Match(text);
            if (servingSizeMatch.Success)
            {
                info.ServingSize = servingSizeMatch.Groups[1].Value.Trim();
                _logger.LogDebug("   → Porção: {ServingSize}", info.ServingSize);
            }

            // Extrair calorias
            var caloriesMatch = CaloriesPattern.Match(text);
            if (caloriesMatch.Success && TryParseDouble(caloriesMatch.Groups[1].Value, out var calories))
            {
                info.Calories = calories;
                _logger.LogDebug("   → Calorias: {Calories} kcal", info.Calories);
            }

            // Extrair carboidratos
            var carbsMatch = CarbohydratesPattern.Match(text);
            if (carbsMatch.Success && TryParseDouble(carbsMatch.Groups[1].Value, out var carbs))
            {
                info.Carbohydrates = carbs;
                _logger.LogDebug("   → Carboidratos: {Carbs} g", info.Carbohydrates);
            }

            // Extrair proteínas
            var proteinsMatch = ProteinsPattern.Match(text);
            if (proteinsMatch.Success && TryParseDouble(proteinsMatch.Groups[1].Value, out var proteins))
            {
                info.Proteins = proteins;
                _logger.LogDebug("   → Proteínas: {Proteins} g", info.Proteins);
            }

            // Extrair gorduras totais
            var totalFatMatch = TotalFatPattern.Match(text);
            if (totalFatMatch.Success && TryParseDouble(totalFatMatch.Groups[1].Value, out var totalFat))
            {
                info.TotalFat = totalFat;
                _logger.LogDebug("   → Gorduras Totais: {Fat} g", info.TotalFat);
            }

            // Extrair gorduras saturadas
            var saturatedFatMatch = SaturatedFatPattern.Match(text);
            if (saturatedFatMatch.Success && TryParseDouble(saturatedFatMatch.Groups[1].Value, out var saturatedFat))
            {
                info.SaturatedFat = saturatedFat;
                _logger.LogDebug("   → Gorduras Saturadas: {Fat} g", info.SaturatedFat);
            }

            // Extrair gorduras trans
            var transFatMatch = TransFatPattern.Match(text);
            if (transFatMatch.Success && TryParseDouble(transFatMatch.Groups[1].Value, out var transFat))
            {
                info.TransFat = transFat;
                _logger.LogDebug("   → Gorduras Trans: {Fat} g", info.TransFat);
            }

            // Extrair fibras
            var fiberMatch = FiberPattern.Match(text);
            if (fiberMatch.Success && TryParseDouble(fiberMatch.Groups[1].Value, out var fiber))
            {
                info.Fiber = fiber;
                _logger.LogDebug("   → Fibras: {Fiber} g", info.Fiber);
            }

            // Extrair sódio
            var sodiumMatch = SodiumPattern.Match(text);
            if (sodiumMatch.Success && TryParseDouble(sodiumMatch.Groups[1].Value, out var sodium))
            {
                info.Sodium = sodium;
                _logger.LogDebug("   → Sódio: {Sodium} mg", info.Sodium);
            }

            // Extrair açúcares
            var sugarsMatch = SugarsPattern.Match(text);
            if (sugarsMatch.Success && TryParseDouble(sugarsMatch.Groups[1].Value, out var sugars))
            {
                info.Sugars = sugars;
                _logger.LogDebug("   → Açúcares: {Sugars} g", info.Sugars);
            }

            return info;
        }

        private bool HasMinimumNutritionData(NutritionalInformationDto info)
        {
            // Pelo menos 2 dos 3 principais nutrientes devem estar presentes
            var mainNutrientsCount = 0;
            if (info.Calories.HasValue) mainNutrientsCount++;
            if (info.Carbohydrates.HasValue) mainNutrientsCount++;
            if (info.Proteins.HasValue) mainNutrientsCount++;
            if (info.TotalFat.HasValue) mainNutrientsCount++;

            return mainNutrientsCount >= 2;
        }

        private int CountExtractedFields(NutritionalInformationDto info)
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(info.ServingSize)) count++;
            if (info.ServingsPerContainer.HasValue) count++;
            if (info.Calories.HasValue) count++;
            if (info.Carbohydrates.HasValue) count++;
            if (info.Proteins.HasValue) count++;
            if (info.TotalFat.HasValue) count++;
            if (info.SaturatedFat.HasValue) count++;
            if (info.TransFat.HasValue) count++;
            if (info.Fiber.HasValue) count++;
            if (info.Sodium.HasValue) count++;
            if (info.Sugars.HasValue) count++;
            return count;
        }

        private double CalculateCompletenessScore(NutritionalInformationDto info)
        {
            // Score baseado na presença de campos importantes
            const double maxFields = 11.0; // Total de campos possíveis
            var extractedFields = CountExtractedFields(info);

            return extractedFields / maxFields;
        }

        private bool TryParseDouble(string value, out double result)
        {
            // Normalizar separador decimal
            value = value.Replace(',', '.');
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}
