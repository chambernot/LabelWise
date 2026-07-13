using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Parser refinado para tabelas nutricionais com suporte robusto a OCR quebrado.
    /// 
    /// CARACTERÍSTICAS:
    /// - Suporta números com vírgula e ponto (1,5 ou 1.5)
    /// - Lida com OCR quebrado em múltiplas linhas
    /// - Extrai todos os campos nutricionais principais
    /// - Valida consistência dos dados extraídos
    /// - Garante que nutritionalFacts não seja null se houver dados válidos
    /// </summary>
    public class NutritionTableParser : INutritionTableParser
    {
        private static readonly CultureInfo Culture = new("pt-BR");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // REGEX PATTERNS - Otimizados para OCR real
        // ═══════════════════════════════════════════════════════════════════════════════════════

        // Aceita múltiplos formatos: "1,5g", "1.5 g", "1,5 g", "1.5g"
        private static readonly Regex NumericValueRegex = new(
            @"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>g|mg|kcal|kj|ml)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Porção: "30g", "200ml", "3 unidades", "2 colheres"
        private static readonly Regex ServingSizeRegex = new(
            @"(?:por[çc][ãa]o|serving)\s*[:\-]?\s*(?<value>\d+\s*(?:g|ml|mg|unidade[s]?|fatia[s]?|colher[es]?|biscoito[s]?|scoop[s]?))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Porções por embalagem: "10 porções", "aproximadamente 15 porções"
        private static readonly Regex ServingsPerContainerRegex = new(
            @"(?:aproximadamente\s+)?(?<value>\d+)\s*(?:por[çc][õo]es?|servings?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // MÉTODO PRINCIPAL DE PARSING
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public NutritionTableParseResult Parse(string ocrText)
        {
            var result = new NutritionTableParseResult();

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Texto OCR da tabela nutricional está vazio.");
                return result;
            }

            // Normalizar texto (melhor para OCR quebrado)
            var normalizedText = NormalizeOcrText(ocrText);
            var lines = SplitIntoLines(normalizedText);

            // ═══════════════════════════════════════════════════════════════════════════════════
            // EXTRAÇÃO DE CAMPOS
            // ═══════════════════════════════════════════════════════════════════════════════════

            // Porção e servings
            result.ServingSize = ExtractServingSize(normalizedText, lines);
            result.ServingsPerContainer = ExtractServingsPerContainer(normalizedText, lines);

            // Energia
            result.Calories = ExtractNumericField(lines, normalizedText, 
                new[] { "valor energ[eé]tico", "energia", "energy", "calorias", "calories" },
                new[] { "kcal" });

            // Carboidratos e açúcares
            result.TotalCarbohydrate = ExtractNumericField(lines, normalizedText,
                new[] { "carboidrato[s]?", "carbohydrate[s]?" },
                new[] { "g" });

            result.Sugars = ExtractNumericField(lines, normalizedText,
                new[] { "a[çc][úu]car[es]?", "sugar[s]?" },
                new[] { "g" });

            result.AddedSugars = ExtractNumericField(lines, normalizedText,
                new[] { "a[çc][úu]car[es]?\\s+adicionad[oa][s]?", "added sugar[s]?" },
                new[] { "g" });

            result.Lactose = ExtractNumericField(lines, normalizedText,
                new[] { "lactose" },
                new[] { "g" });

            // Proteínas
            result.Protein = ExtractNumericField(lines, normalizedText,
                new[] { "prote[íi]na[s]?", "protein[s]?" },
                new[] { "g" });

            // Gorduras
            result.TotalFat = ExtractNumericField(lines, normalizedText,
                new[] { "gordura[s]?\\s+total[is]?", "total\\s+fat", "gorduras?" },
                new[] { "g" });

            result.SaturatedFat = ExtractNumericField(lines, normalizedText,
                new[] { "gordura[s]?\\s+saturada[s]?", "saturated\\s+fat" },
                new[] { "g" });

            result.TransFat = ExtractNumericField(lines, normalizedText,
                new[] { "gordura[s]?\\s+trans", "trans\\s+fat" },
                new[] { "g" });

            // Fibras
            result.DietaryFiber = ExtractNumericField(lines, normalizedText,
                new[] { "fibra[s]?\\s+alimentar[es]?", "dietary\\s+fiber", "fibra[s]?" },
                new[] { "g" });

            // Sódio
            result.Sodium = ExtractNumericField(lines, normalizedText,
                new[] { "s[óo]dio", "sodium" },
                new[] { "mg" });

            // Cálcio
            result.Calcium = ExtractNumericField(lines, normalizedText,
                new[] { "c[áa]lcio", "calcium" },
                new[] { "mg" });

            // Creatina (suplementos)
            result.Creatine = ExtractCreatine(lines, normalizedText);

            // ═══════════════════════════════════════════════════════════════════════════════════
            // VALIDAÇÃO E CONFIANÇA
            // ═══════════════════════════════════════════════════════════════════════════════════

            ValidateExtractedData(result);
            CalculateConfidence(result);

            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // HELPER METHODS - NORMALIZAÇÃO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Normaliza texto OCR para facilitar parsing:
        /// - Remove caracteres especiais problemáticos
        /// - Unifica espaços múltiplos
        /// - Corrige separadores decimais comuns de OCR
        /// </summary>
        private static string NormalizeOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Remover caracteres de controle
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", " ");

            // Unificar quebras de linha
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Corrigir separadores decimais comuns do OCR
            text = Regex.Replace(text, @"(\d)\s*[,.]\s*(\d)", "$1,$2"); // "1 , 5" -> "1,5"

            // Unificar espaços múltiplos
            text = Regex.Replace(text, @"\s+", " ");

            return text;
        }

        /// <summary>
        /// Divide texto em linhas, preservando contexto para OCR quebrado.
        /// </summary>
        private static List<string> SplitIntoLines(string text)
        {
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // HELPER METHODS - EXTRAÇÃO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extrai tamanho da porção (ex: "30g", "200ml", "3 biscoitos").
        /// </summary>
        private static string ExtractServingSize(string fullText, List<string> lines)
        {
            // Tenta primeiro no texto completo (melhor para OCR quebrado)
            var match = ServingSizeRegex.Match(fullText);
            if (match.Success)
            {
                return match.Groups["value"].Value.Trim();
            }

            // Fallback: busca linha por linha
            var keywords = new[] { "por[çc][ãa]o", "serving" };
            foreach (var keyword in keywords)
            {
                var pattern = new Regex(keyword + @"\s*[:\-]?\s*(\d+\s*(?:g|ml|mg|unidade[s]?|fatia[s]?|colher[es]?|biscoito[s]?|scoop[s]?))",
                    RegexOptions.IgnoreCase);

                foreach (var line in lines)
                {
                    var lineMatch = pattern.Match(line);
                    if (lineMatch.Success)
                    {
                        return lineMatch.Groups[1].Value.Trim();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extrai número de porções por embalagem.
        /// </summary>
        private static int? ExtractServingsPerContainer(string fullText, List<string> lines)
        {
            var match = ServingsPerContainerRegex.Match(fullText);
            if (match.Success && int.TryParse(match.Groups["value"].Value, out var servings))
            {
                return servings;
            }

            return null;
        }

        /// <summary>
        /// Extrai valor numérico usando múltiplas estratégias (ideal para OCR quebrado).
        /// </summary>
        private static double? ExtractNumericField(List<string> lines, string fullText, string[] keywordPatterns, string[] expectedUnits = null)
        {
            // Estratégia 1: Busca no texto completo (melhor para OCR quebrado entre linhas)
            foreach (var keywordPattern in keywordPatterns)
            {
                var pattern = new Regex(
                    keywordPattern + @"\s*[:\-]?\s*(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>g|mg|kcal|kj|ml)?",
                    RegexOptions.IgnoreCase);

                var match = pattern.Match(fullText);
                if (match.Success)
                {
                    var value = ParseNumericValue(match.Groups["value"].Value);
                    var unit = match.Groups["unit"].Value;

                    if (ValidateUnit(unit, expectedUnits) && value.HasValue)
                    {
                        // Ignora valores que são claramente %VD
                        if (value.Value > 0 && !IsLikelyPercentage(match.Value))
                        {
                            return value;
                        }
                    }
                }
            }

            // Estratégia 2: Busca linha por linha
            foreach (var keywordPattern in keywordPatterns)
            {
                foreach (var line in lines)
                {
                    if (Regex.IsMatch(line, keywordPattern, RegexOptions.IgnoreCase))
                    {
                        var valueMatch = NumericValueRegex.Match(line);
                        if (valueMatch.Success)
                        {
                            var value = ParseNumericValue(valueMatch.Groups["value"].Value);
                            var unit = valueMatch.Groups["unit"].Value;

                            if (ValidateUnit(unit, expectedUnits) && value.HasValue)
                            {
                                if (value.Value > 0 && !IsLikelyPercentage(line))
                                {
                                    return value;
                                }
                            }
                        }
                    }
                }
            }

            // Estratégia 3: Multi-linha (keyword em uma linha, valor na próxima)
            for (int i = 0; i < lines.Count - 1; i++)
            {
                foreach (var keywordPattern in keywordPatterns)
                {
                    if (Regex.IsMatch(lines[i], keywordPattern, RegexOptions.IgnoreCase))
                    {
                        var valueMatch = NumericValueRegex.Match(lines[i + 1]);
                        if (valueMatch.Success)
                        {
                            var value = ParseNumericValue(valueMatch.Groups["value"].Value);
                            var unit = valueMatch.Groups["unit"].Value;

                            if (ValidateUnit(unit, expectedUnits) && value.HasValue)
                            {
                                if (value.Value > 0 && !IsLikelyPercentage(lines[i + 1]))
                                {
                                    return value;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extrai creatina com tratamento especial para g/mg.
        /// </summary>
        private static double? ExtractCreatine(List<string> lines, string fullText)
        {
            var creatineValue = ExtractNumericField(lines, fullText,
                new[] { "creatina", "creatine" },
                new[] { "g", "mg" });

            if (!creatineValue.HasValue) return null;

            // Detectar unidade para conversão
            var creatineLine = lines.FirstOrDefault(l => 
                Regex.IsMatch(l, @"creatina", RegexOptions.IgnoreCase));

            if (creatineLine != null)
            {
                // Se encontrar explicitamente "g" (sem "mg"), converter para mg
                if (Regex.IsMatch(creatineLine, @"\d+(?:[.,]\d+)?\s*g\b", RegexOptions.IgnoreCase) &&
                    !creatineLine.ToLower().Contains("mg"))
                {
                    return creatineValue * 1000; // g -> mg
                }
            }

            return creatineValue;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // HELPER METHODS - VALIDAÇÃO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parse de valor numérico com suporte a vírgula e ponto.
        /// </summary>
        private static double? ParseNumericValue(string valueStr)
        {
            if (string.IsNullOrWhiteSpace(valueStr)) return null;

            // Normalizar: substituir vírgula por ponto
            valueStr = valueStr.Replace(",", ".");

            if (double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Valida se a unidade encontrada corresponde à esperada.
        /// </summary>
        private static bool ValidateUnit(string foundUnit, string[] expectedUnits)
        {
            if (expectedUnits == null || expectedUnits.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(foundUnit)) return true; // Sem unidade = aceita

            return expectedUnits.Any(u => u.Equals(foundUnit, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Detecta se um valor é provavelmente uma porcentagem (%VD).
        /// </summary>
        private static bool IsLikelyPercentage(string text)
        {
            return text.Contains('%') || text.Contains("VD") || text.Contains("DV");
        }

        /// <summary>
        /// Valida consistência dos dados extraídos.
        /// </summary>
        private static void ValidateExtractedData(NutritionTableParseResult result)
        {
            // Validação: açúcar adicionado não pode ser maior que açúcar total
            if (result.AddedSugars.HasValue && result.Sugars.HasValue)
            {
                if (result.AddedSugars.Value > result.Sugars.Value)
                {
                    result.ValidationWarnings.Add(
                        $"Açúcar adicionado ({result.AddedSugars}g) maior que açúcar total ({result.Sugars}g)");
                }
            }

            // Validação: gordura saturada não pode ser maior que gordura total
            if (result.SaturatedFat.HasValue && result.TotalFat.HasValue)
            {
                if (result.SaturatedFat.Value > result.TotalFat.Value)
                {
                    result.ValidationWarnings.Add(
                        $"Gordura saturada ({result.SaturatedFat}g) maior que gordura total ({result.TotalFat}g)");
                }
            }

            // Validação: soma de macros não deve exceder muito 100g
            if (result.TotalCarbohydrate.HasValue && result.Protein.HasValue && result.TotalFat.HasValue)
            {
                var macroSum = result.TotalCarbohydrate.Value + result.Protein.Value + result.TotalFat.Value;
                if (macroSum > 110) // Margem de 10% para fibras/água
                {
                    result.ValidationWarnings.Add(
                        $"Soma de macronutrientes ({macroSum:F1}g) excede 110g - possível erro de parsing");
                }
            }
        }

        /// <summary>
        /// Calcula nível de confiança baseado na quantidade e qualidade dos dados extraídos.
        /// </summary>
        private static void CalculateConfidence(NutritionTableParseResult result)
        {
            if (!result.HasNutritionData)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nenhum campo nutricional pôde ser extraído da tabela.");
                return;
            }

            var fieldsCount = result.ExtractedFieldsCount;

            // Confiança baseada em quantidade de campos
            if (fieldsCount >= 8)
            {
                result.Confidence = ConfidenceLevel.High;
            }
            else if (fieldsCount >= 4)
            {
                result.Confidence = ConfidenceLevel.Medium;
            }
            else
            {
                result.Confidence = ConfidenceLevel.Low;
            }

            // Reduzir confiança se houver muitos warnings
            if (result.ValidationWarnings.Count >= 3)
            {
                result.Confidence = result.Confidence == ConfidenceLevel.High 
                    ? ConfidenceLevel.Medium 
                    : ConfidenceLevel.Low;
            }
        }
    }
}
