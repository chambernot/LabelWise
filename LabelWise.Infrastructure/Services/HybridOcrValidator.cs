using System.Globalization;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Validador híbrido que usa Azure Computer Vision OCR para validar valores críticos
/// extraídos pelo Azure OpenAI Vision, corrigindo quando há divergências significativas.
/// 
/// ESTRATÉGIA DE VALIDAÇÃO:
/// 1. OpenAI Vision extrai dados com contexto semântico (melhor para layout complexo)
/// 2. Azure Computer Vision OCR valida números (mais preciso para valores numéricos)
/// 3. Se divergência > 15% E OCR for mais consistente: CORRIGE com OCR
/// 4. Validação baseada na Regra 4-4-9 de consistência calórica
/// 
/// REGRA 4-4-9:
/// Calorias = (Carbs × 4) + (Protein × 4) + (Fat × 9)
/// Tolerância: ±10% (fibras e conversões)
/// 
/// IMPORTANTE: Computer Vision OCR é mais preciso para NÚMEROS, mesmo que OpenAI
/// seja melhor para CONTEXTO. Quando há divergência numérica, OCR geralmente está correto.
/// </summary>
public class HybridOcrValidator : IHybridOcrValidator
{
    private readonly IOcrProvider _azureVisionOcr;
    private readonly ILogger<HybridOcrValidator> _logger;
    private const double DivergenceThreshold = 0.15; // 15% de divergência
    private const double ConsistencyTolerance = 0.10; // 10% tolerância para consistência calórica

    public HybridOcrValidator(
        IOcrProvider azureVisionOcr,
        ILogger<HybridOcrValidator> logger)
    {
        _azureVisionOcr = azureVisionOcr;
        _logger = logger;
    }

    public async Task<bool> ValidateAndCorrectAsync(
        EstimatedNutritionProfileDto profile,
        string imagePath,
        List<string> warnings)
    {
        if (profile == null || string.IsNullOrWhiteSpace(imagePath))
            return false;

        try
        {
            _logger.LogInformation("[HYBRID_OCR] Starting validation with Azure Computer Vision");

            var ocrRequest = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            var ocrResult = await _azureVisionOcr.ExtractTextAsync(ocrRequest);
            if (!ocrResult.Success || string.IsNullOrWhiteSpace(ocrResult.RawText))
            {
                _logger.LogWarning("[HYBRID_OCR] Computer Vision OCR failed or returned empty");
                return false;
            }

            _logger.LogInformation("[HYBRID_OCR] OCR extracted {Lines} lines with confidence {Confidence:F2}%",
                ocrResult.TextBlocks?.Count ?? 0, ocrResult.Confidence * 100);

            var lines = ocrResult.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var ocrValues = ExtractNutritionValuesFromOcr(lines);

            // ═══════════════════════════════════════════════════════════════
            // VALIDAÇÃO DE CONSISTÊNCIA NUTRICIONAL (REGRA 4-4-9)
            // ═══════════════════════════════════════════════════════════════

            var openAiConsistency = ValidateNutritionalConsistency(profile, "OpenAI Vision");
            var ocrConsistency = ValidateNutritionalConsistency(CreateOcrProfile(profile, ocrValues), "Computer Vision OCR");

            _logger.LogInformation("[HYBRID_OCR] Consistency check:");
            _logger.LogInformation("[HYBRID_OCR]    OpenAI Vision: {OpenAIConsistent} (error: {OpenAIError:F1}%)", 
                openAiConsistency.IsConsistent ? "✅ CONSISTENT" : "⚠️ INCONSISTENT", 
                openAiConsistency.ErrorPercentage);
            _logger.LogInformation("[HYBRID_OCR]    Computer Vision OCR: {OCRConsistent} (error: {OCRError:F1}%)", 
                ocrConsistency.IsConsistent ? "✅ CONSISTENT" : "⚠️ INCONSISTENT", 
                ocrConsistency.ErrorPercentage);

            // ═══════════════════════════════════════════════════════════════
            // DECISÃO: Priorizar OCR baseado em DIVERGÊNCIA ABSOLUTA + Consistência
            // ═══════════════════════════════════════════════════════════════

            // REGRA 1: Verificar divergência absoluta nos valores críticos
            // Se houver divergência > 15% em QUALQUER valor, SEMPRE usar OCR
            var criticalDivergences = new List<(string nutrient, double divergence)>();

            foreach (var (key, ocrValue) in ocrValues)
            {
                double? currentValue = key switch
                {
                    "calories" => profile.CaloriesPer100g ?? profile.CaloriesPer100ml,
                    "protein" => profile.EstimatedProteinPer100g,
                    "fat" => profile.EstimatedFatPer100g,
                    "carbs" => profile.EstimatedCarbsPer100g,
                    _ => null
                };

                if (currentValue.HasValue && ocrValue.col100.HasValue && currentValue.Value > 0)
                {
                    var divergence = Math.Abs(currentValue.Value - ocrValue.col100.Value) / currentValue.Value;
                    if (divergence > DivergenceThreshold) // > 15%
                    {
                        criticalDivergences.Add((key, divergence));
                    }
                }
            }

            bool hasCriticalDivergence = criticalDivergences.Count > 0;

            if (hasCriticalDivergence)
            {
                _logger.LogWarning("[HYBRID_OCR] ⚠️ CRITICAL DIVERGENCE detected:");
                foreach (var (nutrient, divergence) in criticalDivergences)
                {
                    _logger.LogWarning("[HYBRID_OCR]    - {Nutrient}: {Divergence:P1}", nutrient, divergence);
                }
                _logger.LogWarning("[HYBRID_OCR] → Using OCR as primary source (more literal/precise)");

                // SEMPRE usar OCR quando houver divergência crítica
                // (OCR é mais literal e preciso para números)
            }
            else
            {
                // REGRA 2: Sem divergência crítica, usar lógica de consistência
                if (ocrConsistency.IsConsistent && !openAiConsistency.IsConsistent)
                {
                    _logger.LogWarning("[HYBRID_OCR] ⚠️ OCR is consistent but OpenAI is not - will apply OCR corrections");
                }
                else if (!ocrConsistency.IsConsistent && openAiConsistency.IsConsistent)
                {
                    _logger.LogInformation("[HYBRID_OCR] ✅ OpenAI is consistent, OCR is not - keeping OpenAI values");
                    warnings.Add("✅ Valores nutricionais validados e consistentes (OpenAI Vision)");
                    return false; // NÃO corrige
                }
                else if (ocrConsistency.IsConsistent && openAiConsistency.IsConsistent)
                {
                    // Ambos consistentes: usa o que tem menor erro
                    if (ocrConsistency.ErrorPercentage < openAiConsistency.ErrorPercentage)
                    {
                        _logger.LogInformation("[HYBRID_OCR] ✅ Both consistent, but OCR has lower error ({OCRErr:F1}% vs {AIErr:F1}%) - using OCR",
                            ocrConsistency.ErrorPercentage, openAiConsistency.ErrorPercentage);
                    }
                    else
                    {
                        _logger.LogInformation("[HYBRID_OCR] ✅ Both consistent, OpenAI has lower error - keeping OpenAI");
                        warnings.Add("✅ Valores nutricionais validados por dupla checagem (OpenAI + OCR)");
                        return false; // NÃO corrige
                    }
                }
                else
                {
                    // Ambos inconsistentes: tenta usar OCR como fallback
                    _logger.LogWarning("[HYBRID_OCR] ⚠️ Both inconsistent - using OCR as fallback (OCR error: {OCRErr:F1}% vs AI error: {AIErr:F1}%)",
                        ocrConsistency.ErrorPercentage, openAiConsistency.ErrorPercentage);
                }
            }

            // Se chegou aqui, vai aplicar correções do OCR

            var corrected = false;

            corrected |= ValidateAndCorrectCalories(profile, ocrValues, warnings);
            corrected |= ValidateAndCorrectNutrient(profile, ocrValues, "protein", 
                v => profile.EstimatedProteinPer100g, v => profile.EstimatedProteinPer100g = v, "Proteínas", warnings);
            corrected |= ValidateAndCorrectNutrient(profile, ocrValues, "fat", 
                v => profile.EstimatedFatPer100g, v => profile.EstimatedFatPer100g = v, "Gorduras totais", warnings);
            corrected |= ValidateAndCorrectNutrient(profile, ocrValues, "carbs", 
                v => profile.EstimatedCarbsPer100g, v => profile.EstimatedCarbsPer100g = v, "Carboidratos", warnings);
            corrected |= ValidateAndCorrectNutrient(profile, ocrValues, "sodium", 
                v => profile.EstimatedSodiumPer100g, v => profile.EstimatedSodiumPer100g = v, "Sódio", warnings);

            if (corrected)
            {
                profile.DataSource ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                profile.DataSource["ValidationMethod"] = "Azure Computer Vision OCR";
                _logger.LogInformation("[HYBRID_OCR] ✅ Corrections applied successfully");
            }
            else
            {
                _logger.LogInformation("[HYBRID_OCR] ✅ All values validated, no corrections needed");
            }

            return corrected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HYBRID_OCR] Validation failed, keeping original values");
            return false;
        }
    }

    private bool ValidateAndCorrectCalories(
        EstimatedNutritionProfileDto profile,
        Dictionary<string, (double? col100, double? colPortion)> ocrValues,
        List<string> warnings)
    {
        if (!ocrValues.TryGetValue("calories", out var ocrCalories))
            return false;

        var currentValue = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        var ocrValue = ocrCalories.col100;

        if (!currentValue.HasValue || !ocrValue.HasValue)
            return false;

        var divergence = Math.Abs(currentValue.Value - ocrValue.Value) / currentValue.Value;

        if (divergence > DivergenceThreshold)
        {
            _logger.LogWarning("[HYBRID_OCR] Calories divergence detected: AI={AI}, OCR={OCR}, Divergence={Div:P}",
                currentValue.Value, ocrValue.Value, divergence);

            var unit = profile.NutritionUnit ?? "g";
            if (unit == "ml")
            {
                profile.CaloriesPer100ml = ocrValue;
                profile.CaloriesPer100g = null;
            }
            else
            {
                profile.CaloriesPer100g = ocrValue;
                profile.CaloriesPer100ml = null;
            }

            warnings.Add($"⚠️ Calorias corrigidas de {currentValue.Value:F0} para {ocrValue.Value:F0} kcal usando OCR de validação");

            if (profile.DataSource != null)
                profile.DataSource["CaloriesSource"] = "Azure Computer Vision OCR (corrected)";

            return true;
        }

        return false;
    }

    private bool ValidateAndCorrectNutrient(
        EstimatedNutritionProfileDto profile,
        Dictionary<string, (double? col100, double? colPortion)> ocrValues,
        string key,
        Func<EstimatedNutritionProfileDto, double?> getter,
        Action<double?> setter,
        string displayName,
        List<string> warnings)
    {
        if (!ocrValues.TryGetValue(key, out var ocrValue))
            return false;

        var currentValue = getter(profile);
        var ocrVal = ocrValue.col100;

        if (!currentValue.HasValue || !ocrVal.HasValue)
            return false;

        if (currentValue.Value == 0 && ocrVal.Value == 0)
            return false;

        var divergence = currentValue.Value > 0 
            ? Math.Abs(currentValue.Value - ocrVal.Value) / currentValue.Value
            : (ocrVal.Value > 0 ? 1.0 : 0.0);

        if (divergence > DivergenceThreshold)
        {
            _logger.LogWarning("[HYBRID_OCR] {Nutrient} divergence detected: AI={AI}, OCR={OCR}, Divergence={Div:P}",
                displayName, currentValue.Value, ocrVal.Value, divergence);

            setter(ocrVal);
            warnings.Add($"⚠️ {displayName} corrigido de {currentValue.Value:F1} para {ocrVal.Value:F1} usando OCR de validação");

            if (profile.DataSource != null)
                profile.DataSource[$"{key}Source"] = "Azure Computer Vision OCR (corrected)";

            return true;
        }

        return false;
    }

    private Dictionary<string, (double? col100, double? colPortion)> ExtractNutritionValuesFromOcr(string[] lines)
    {
        var result = new Dictionary<string, (double? col100, double? colPortion)>(StringComparer.OrdinalIgnoreCase);

        // ESTRATÉGIA 1: Tentar extrair valores da mesma linha (tabela inline)
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var key = MapNutrientKey(line);
            if (key == null)
                continue;

            var numbers = ExtractNumbers(line);
            if (numbers.Count >= 2)
            {
                result[key] = (numbers[0], numbers[1]);
                _logger.LogDebug("[HYBRID_OCR] Extracted {Key} from same line: 100g={Val100}, portion={ValPortion}",
                    key, numbers[0], numbers[1]);
            }
        }

        // Se conseguiu extrair valores, retornar
        if (result.Count > 0)
        {
            _logger.LogInformation("[HYBRID_OCR] ✅ Extracted {Count} nutrients using inline strategy", result.Count);
            return result;
        }

        // ESTRATÉGIA 2: Tabela estruturada (valores em linhas separadas)
        // Usar parser estruturado para entender a tabela completa
        _logger.LogInformation("[HYBRID_OCR] Trying structured table parsing...");
        _logger.LogInformation("[HYBRID_OCR] Total lines to parse: {Count}", lines.Length);

        // Log primeiras 50 linhas para debug
        for (int i = 0; i < Math.Min(50, lines.Length); i++)
        {
            _logger.LogDebug("[HYBRID_OCR] Line {Index}: {Line}", i, lines[i]);
        }

        var parser = new NutritionTableParser();
        var parsed = parser.Parse(lines.ToList());

        _logger.LogInformation("[HYBRID_OCR] Parser result - HasAnyValue: {HasAny}", parsed.HasAnyValue);
        _logger.LogInformation("[HYBRID_OCR] Parser result - Calories: {Cal}, Carbs: {Carbs}, Protein: {Prot}, Fat: {Fat}",
            parsed.Calories, parsed.Carbs, parsed.Protein, parsed.Fat);

        if (parsed.HasAnyValue)
        {
            // Só adicionar valores que não são nulos
            if (parsed.Calories.HasValue)
                result["calories"] = (parsed.Calories, null);
            if (parsed.Carbs.HasValue)
                result["carbs"] = (parsed.Carbs, null);
            if (parsed.Protein.HasValue)
                result["protein"] = (parsed.Protein, null);
            if (parsed.Fat.HasValue)
                result["fat"] = (parsed.Fat, null);
            if (parsed.SaturatedFat.HasValue)
                result["saturated_fat"] = (parsed.SaturatedFat, null);
            if (parsed.Fiber.HasValue)
                result["fiber"] = (parsed.Fiber, null);
            if (parsed.Sodium.HasValue)
                result["sodium"] = (parsed.Sodium, null);
            if (parsed.Sugar.HasValue)
                result["sugar"] = (parsed.Sugar, null);

            var extractedCount = result.Count(kv => kv.Value.col100.HasValue);
            _logger.LogInformation("[HYBRID_OCR] ✅ Extracted {Count} nutrients using structured parser", extractedCount);

            // Log valores extraídos para debug
            foreach (var (key, value) in result.Where(kv => kv.Value.col100.HasValue))
            {
                _logger.LogInformation("[HYBRID_OCR] Extracted {Key}: 100g={Val100}",
                    key, value.col100);
            }
        }
        else
        {
            _logger.LogWarning("[HYBRID_OCR] ⚠️ Parser returned no values. Trying FALLBACK strategy...");

            // ESTRATÉGIA 3: FALLBACK - Busca manual por padrões conhecidos
            result = ExtractNutrientsManually(lines);

            if (result.Count > 0)
            {
                _logger.LogInformation("[HYBRID_OCR] ✅ Extracted {Count} nutrients using manual fallback", result.Count);
            }
            else
            {
                _logger.LogWarning("[HYBRID_OCR] ❌ All extraction strategies failed");
            }
        }

        return result;
    }

    private string? MapNutrientKey(string line)
    {
        var normalized = Normalize(line);

        if (normalized.Contains("valorenergetico") || normalized.Contains("energetico") || normalized.Contains("energ"))
            return "calories";
        if (normalized.Contains("carboidrato"))
            return "carbs";
        if (normalized.Contains("acucar") && !normalized.Contains("adicionado"))
            return "sugar";
        if (normalized.Contains("proteina"))
            return "protein";
        if (normalized.Contains("gordura") && !normalized.Contains("saturad") && !normalized.Contains("trans"))
            return "fat";
        if (normalized.Contains("gordura") && normalized.Contains("saturad"))
            return "saturated_fat";
        if (normalized.Contains("fibra"))
            return "fiber";
        if (normalized.Contains("sodio"))
            return "sodium";

        return null;
    }

    private List<double> ExtractNumbers(string line)
    {
        var values = new List<double>();
        var matches = Regex.Matches(line, @"\d+(?:[,\.]\d+)?");

        foreach (Match match in matches)
        {
            var raw = match.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.ToLowerInvariant()
            .Replace("á", "a")
            .Replace("â", "a")
            .Replace("ã", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");

        return Regex.Replace(normalized, @"\s+", "");
    }

    /// <summary>
    /// Valida consistência nutricional usando a regra 4-4-9:
    /// Calorias = (Carboidratos × 4) + (Proteínas × 4) + (Gorduras × 9)
    /// Tolerância: ±10% (devido a fibras, conversões e arredondamentos)
    /// </summary>
    private (bool IsConsistent, double ErrorPercentage, double CalculatedCalories) ValidateNutritionalConsistency(
        EstimatedNutritionProfileDto profile, 
        string source)
    {
        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        var carbs = profile.EstimatedCarbsPer100g ?? 0;
        var protein = profile.EstimatedProteinPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;

        if (!calories.HasValue || calories.Value == 0)
            return (false, 100, 0);

        // Regra 4-4-9
        var calculatedCalories = (carbs * 4) + (protein * 4) + (fat * 9);
        var error = Math.Abs(calories.Value - calculatedCalories) / calories.Value;

        _logger.LogDebug("[HYBRID_OCR] {Source} - Declared: {Declared} kcal, Calculated: {Calculated:F1} kcal (C:{Carbs}g P:{Protein}g F:{Fat}g)",
            source, calories.Value, calculatedCalories, carbs, protein, fat);

        var isConsistent = error <= ConsistencyTolerance;
        return (isConsistent, error * 100, calculatedCalories);
    }

    /// <summary>
    /// Cria um profile com valores do OCR para validar consistência
    /// </summary>
    private EstimatedNutritionProfileDto CreateOcrProfile(
        EstimatedNutritionProfileDto originalProfile,
        Dictionary<string, (double? col100, double? colPortion)> ocrValues)
    {
        var ocrProfile = new EstimatedNutritionProfileDto
        {
            NutritionUnit = originalProfile.NutritionUnit
        };

        if (ocrValues.TryGetValue("calories", out var calories))
        {
            if (originalProfile.NutritionUnit == "ml")
                ocrProfile.CaloriesPer100ml = calories.col100;
            else
                ocrProfile.CaloriesPer100g = calories.col100;
        }

        if (ocrValues.TryGetValue("carbs", out var carbs))
            ocrProfile.EstimatedCarbsPer100g = carbs.col100;

        if (ocrValues.TryGetValue("protein", out var protein))
            ocrProfile.EstimatedProteinPer100g = protein.col100;

        if (ocrValues.TryGetValue("fat", out var fat))
            ocrProfile.EstimatedFatPer100g = fat.col100;

        return ocrProfile;
    }

    private Dictionary<string, string> GetMetadata()
    {
        return new Dictionary<string, string>
        {
            ["Provider"] = "Azure Computer Vision (Hybrid Validator)",
            ["ValidationType"] = "Cross-validation with OpenAI Vision"
        };
    }

    /// <summary>
    /// Estratégia de fallback manual para extrair nutrientes quando o parser estruturado falha.
    /// Busca por padrões conhecidos no texto OCR linha por linha.
    /// </summary>
    private Dictionary<string, (double? col100, double? colPortion)> ExtractNutrientsManually(string[] lines)
    {
        var result = new Dictionary<string, (double? col100, double? colPortion)>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("[HYBRID_OCR] 🔧 Starting manual extraction fallback...");

        // Buscar padrões conhecidos no texto OCR
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var normalized = Normalize(line);

            // Detectar nutriente e tentar pegar valores das próximas linhas
            string? nutrientKey = null;

            if (normalized.Contains("valorenergetico") || normalized.Contains("energetico"))
                nutrientKey = "calories";
            else if (normalized.Contains("carboidrato"))
                nutrientKey = "carbs";
            else if (normalized.Contains("proteina"))
                nutrientKey = "protein";
            else if (normalized.Contains("gordura") && !normalized.Contains("saturad") && !normalized.Contains("trans"))
                nutrientKey = "fat";
            else if (normalized.Contains("gordura") && normalized.Contains("saturad"))
                nutrientKey = "saturated_fat";
            else if (normalized.Contains("fibra"))
                nutrientKey = "fiber";
            else if (normalized.Contains("sodio"))
                nutrientKey = "sodium";

            if (nutrientKey != null)
            {
                // Tentar extrair números da mesma linha
                var numbers = ExtractNumbers(line);

                if (numbers.Count >= 2)
                {
                    result[nutrientKey] = (numbers[0], numbers[1]);
                    _logger.LogInformation("[HYBRID_OCR] 📍 Found {Key} in line {Index}: {Val1}, {Val2}",
                        nutrientKey, i, numbers[0], numbers[1]);
                }
                else if (numbers.Count == 1)
                {
                    // Se só tem 1 número, pegar próximas linhas
                    var nextNumbers = TryGetNumbersFromNextLines(lines, i + 1, 3);
                    if (nextNumbers.Count > 0)
                    {
                        result[nutrientKey] = (numbers[0], nextNumbers.Count > 0 ? nextNumbers[0] : null);
                        _logger.LogInformation("[HYBRID_OCR] 📍 Found {Key} across lines {Index}: {Val1}",
                            nutrientKey, i, numbers[0]);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Tenta extrair números das próximas N linhas
    /// </summary>
    private List<double> TryGetNumbersFromNextLines(string[] lines, int startIndex, int maxLines)
    {
        var numbers = new List<double>();

        for (int i = startIndex; i < Math.Min(startIndex + maxLines, lines.Length); i++)
        {
            var lineNumbers = ExtractNumbers(lines[i]);
            numbers.AddRange(lineNumbers);

            if (numbers.Count >= 2)
                break;
        }

        return numbers;
    }
}
