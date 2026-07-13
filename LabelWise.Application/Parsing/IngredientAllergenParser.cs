using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing
{
    /// <summary>
    /// Parser robusto de rótulos alimentares com validação de nome de produto e detecção de tabela nutricional.
    /// 
    /// ESTRATÉGIA:
    /// 1. Detecta e ignora completamente blocos de tabela nutricional
    /// 2. Extrai nome do produto ANTES da palavra "INGREDIENTES"
    /// 3. Valida nome do produto (não aceita valores numéricos, símbolos ou padrões inválidos)
    /// 4. Reduz confiança se parsing incompleto ou texto com ruído
    /// 5. Retorna null para nome/marca se não houver evidência clara
    /// </summary>
    public class IngredientAllergenParser : IIngredientAllergenParser
    {
        private static readonly string[] CommonAllergens = new[]
        {
            "glúten", "gluten", "leite", "lactose", "soja", "amendoim", "amêndoa", "castanha", "noz", "nozes",
            "peixe", "crustáceo", "camarão", "ovo", "ovos", "trigo", "cevada", "centeio", "aveia"
        };

        private static readonly string[] CriticalTerms = new[]
        {
            "contém", "pode conter", "não contém", "isento de", "sem lactose", "sem glúten", "livre de"
        };

        // ═══════════════════════════════════════════════════════════════════════════════
        // PADRÕES PARA IDENTIFICAR LINHAS DA TABELA NUTRICIONAL (DEVEM SER IGNORADAS)
        // ═══════════════════════════════════════════════════════════════════════════════
        private static readonly string[] NutritionalTableKeywords = new[]
        {
            "INFORMAÇÃO NUTRICIONAL", "INFORMACAO NUTRICIONAL",
            "TABELA NUTRICIONAL", "VALORES NUTRICIONAIS",
            "%VD", "% VD", "VD%",
            "KCAL", "CALORIAS", "VALOR ENERGÉTICO", "VALOR ENERGETICO",
            "PORÇÃO", "PORCAO",
            "CARBOIDRATO", "PROTEÍNA", "PROTEINA", "GORDURA", "LIPÍDIO", "LIPIDIO",
            "SÓDIO", "SODIO", "FIBRA ALIMENTAR",
            "SATURADA", "TRANS", "MONOINSATURADA", "POLINSATURADA",
            "AÇÚCAR", "ACUCAR", "COLESTEROL",
            "POR PORÇÃO", "POR PORCAO", "POR 100G", "POR 100ML"
        };

        private static readonly Regex[] NutritionalTablePatterns = new[]
        {
            new Regex(@"\d+\s*(kcal|cal)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\d+\s*(g|mg|ml|mcg)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\d+\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"%\s*VD", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^\d+$", RegexOptions.Compiled), // apenas números
            new Regex(@"^\d+[.,]\d+$", RegexOptions.Compiled), // apenas números decimais
        };

        // ═══════════════════════════════════════════════════════════════════════════════
        // MÉTODO PRINCIPAL DE PARSING
        // ═══════════════════════════════════════════════════════════════════════════════
        public IngredientAllergenParseResult Parse(string rawOcrText)
        {
            var result = new IngredientAllergenParseResult
            {
                ParsingConfidence = ConfidenceLevel.High
            };

            if (string.IsNullOrWhiteSpace(rawOcrText))
            {
                result.ParsingConfidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Texto OCR vazio ou nulo");
                return result;
            }

            var text = Normalize(rawOcrText);

            // ETAPA 1: Identificar e remover bloco de tabela nutricional
            var (cleanedLines, nutritionalTableStartIndex) = RemoveNutritionalTableBlock(rawOcrText);

            // ETAPA 2: Extrair nome do produto e marca (ANTES de "INGREDIENTES" e fora da tabela nutricional)
            ExtractProductInfoRobust(cleanedLines, nutritionalTableStartIndex, result);

            // ETAPA 3: Extrair informações nutricionais (da seção identificada)
            result.Nutrition = ExtractNutritionInfo(text);

            // ETAPA 4: Extrair ingredientes (após "INGREDIENTES:")
            var ingredientSection = ExtractIngredientsSection(text);
            if (!string.IsNullOrEmpty(ingredientSection))
            {
                var ingredients = SplitIngredients(ingredientSection);
                result.Ingredients.AddRange(ingredients.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => CleanIngredient(i)).Distinct());
            }

            // ETAPA 5: Detectar alergênicos
            DetectAllergens(text, result);

            // ETAPA 6: Extrair frases críticas e classificar alergênicos
            ExtractCriticalPhrases(text, result);

            // ETAPA 7: Validação final e ajuste de confiança
            FinalValidationAndConfidenceAdjustment(result, rawOcrText);

            // Distinct para evitar duplicatas
            result.Allergens = result.Allergens.Distinct().ToList();
            result.ConfirmedAllergens = result.ConfirmedAllergens.Distinct().ToList();
            result.MayContainAllergens = result.MayContainAllergens.Distinct().ToList();
            result.Ingredients = result.Ingredients.Distinct().ToList();
            result.CriticalTerms = result.CriticalTerms.Distinct().ToList();
            result.ExtractedPhrases = result.ExtractedPhrases.Distinct().ToList();

            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 1: IDENTIFICAR E REMOVER BLOCO DE TABELA NUTRICIONAL
        // ═══════════════════════════════════════════════════════════════════════════════
        private static (List<string> cleanedLines, int nutritionalTableStartIndex) RemoveNutritionalTableBlock(string rawText)
        {
            var allLines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var cleanedLines = new List<string>();
            int nutritionalTableStartIndex = -1;
            bool inNutritionalTable = false;

            for (int i = 0; i < allLines.Count; i++)
            {
                var line = allLines[i];
                var normalizedLine = line.ToUpperInvariant();

                // Detectar início da tabela nutricional
                if (!inNutritionalTable && IsNutritionalTableLine(line, normalizedLine))
                {
                    inNutritionalTable = true;
                    nutritionalTableStartIndex = i;
                    continue; // ignora esta linha
                }

                // Se está na tabela nutricional, continuar ignorando até encontrar "INGREDIENTES"
                if (inNutritionalTable)
                {
                    if (normalizedLine.Contains("INGREDIENTES"))
                    {
                        inNutritionalTable = false;
                        cleanedLines.Add(line); // inclui a linha "INGREDIENTES"
                    }
                    continue;
                }

                // Linha válida (não é tabela nutricional)
                cleanedLines.Add(line);
            }

            return (cleanedLines, nutritionalTableStartIndex);
        }

        private static bool IsNutritionalTableLine(string line, string normalizedLine)
        {
            // Verifica keywords
            foreach (var keyword in NutritionalTableKeywords)
            {
                if (normalizedLine.Contains(keyword))
                    return true;
            }

            // Verifica padrões regex
            foreach (var pattern in NutritionalTablePatterns)
            {
                if (pattern.IsMatch(line))
                    return true;
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 2: EXTRAIR NOME DO PRODUTO E MARCA (ROBUSTO)
        // ═══════════════════════════════════════════════════════════════════════════════
        private static void ExtractProductInfoRobust(List<string> cleanedLines, int nutritionalTableStartIndex, IngredientAllergenParseResult result)
        {
            // Pegar apenas as linhas ANTES da tabela nutricional e ANTES de "INGREDIENTES"
            var candidateLines = cleanedLines
                .TakeWhile(line => !line.ToUpperInvariant().Contains("INGREDIENTES"))
                .Take(15) // limite de linhas para buscar nome/marca
                .ToList();

            string? validatedProductName = null;
            string? validatedBrand = null;

            foreach (var line in candidateLines)
            {
                if (IsValidProductName(line))
                {
                    if (validatedProductName == null)
                    {
                        validatedProductName = CleanOcrNoise(line.Trim());
                        result.IsProductNameValidated = true;
                    }
                    else if (validatedBrand == null && line != validatedProductName)
                    {
                        var cleanedBrand = CleanOcrNoise(line.Trim());
                        // Validar que a marca não é keyword inválida
                        if (!IsInvalidBrandKeyword(cleanedBrand))
                        {
                            validatedBrand = cleanedBrand;
                            result.IsBrandValidated = true;
                            break; // já temos nome e marca
                        }
                    }
                }
            }

            result.ProductName = validatedProductName;
            result.Brand = validatedBrand;

            // Adicionar warnings se não encontrou nome válido
            if (validatedProductName == null)
            {
                result.ValidationWarnings.Add("Nenhum nome de produto válido encontrado");
            }
        }

        private static bool IsValidProductName(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var normalized = line.ToUpperInvariant().Trim();

            // Regra 1: Tamanho mínimo de 3 caracteres
            if (line.Trim().Length < 3)
                return false;

            // Regra 2: Não pode ser linha de tabela nutricional
            if (IsNutritionalTableLine(line, normalized))
                return false;

            // Regra 3: Ignorar palavras-chave que não são nome de produto
            var invalidKeywords = new[]
            {
                "INGREDIENTES", "CONTÉM", "PODE CONTER", "NÃO CONTÉM",
                "VALIDADE", "PESO LÍQUIDO", "PESO LIQUIDO", "CONT.",
                "CONSERVAR", "LOTE", "FABRICAÇÃO", "FABRICACAO",
                "SAC", "CNPJ", "INDÚSTRIA", "INDUSTRIA", "LTDA"
            };

            if (invalidKeywords.Any(kw => normalized.Contains(kw)))
                return false;

            // Regra 4: Não pode ser apenas números
            if (Regex.IsMatch(line, @"^\d+$"))
                return false;

            // Regra 5: Não pode ter mais de 60% de números
            var digitCount = line.Count(char.IsDigit);
            var digitPercentage = (double)digitCount / line.Length;
            if (digitPercentage > 0.6)
                return false;

            // Regra 6: Não pode conter muitos símbolos especiais
            var specialChars = line.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
            if (specialChars > line.Length / 3)
                return false;

            // Regra 7: Deve conter pelo menos uma letra
            if (!line.Any(char.IsLetter))
                return false;

            // Regra 8: Comprimento razoável (não muito longo)
            if (line.Length > 100)
                return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 4: EXTRAIR SEÇÃO DE INGREDIENTES
        // ═══════════════════════════════════════════════════════════════════════════════
        private static string ExtractIngredientsSection(string text)
        {
            var patterns = new[] { "ingredientes:", "ingredientes -", "ingredientes", "lista de ingredientes:" };
            foreach (var p in patterns)
            {
                var idx = text.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var start = idx + p.Length;
                    var endIdx = IndexOfAny(text, new[] { "informação nutricional", "informacao nutricional", "tabela nutricional", "alérgicos", "alergicos", "contém:" }, start);
                    if (endIdx < 0) endIdx = text.Length;
                    var section = text.Substring(start, endIdx - start);
                    return section;
                }
            }

            var containIdx = text.IndexOf("contém", StringComparison.OrdinalIgnoreCase);
            if (containIdx >= 0)
            {
                var endIdx = IndexOfAny(text, new[] { ".", "informação nutricional", "informacao nutricional", "tabela nutricional" }, containIdx);
                if (endIdx < 0) endIdx = text.Length;
                return text.Substring(containIdx + "contém".Length, endIdx - (containIdx + "contém".Length));
            }

            return string.Empty;
        }

        private static List<string> SplitIngredients(string section)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(section)) return list;

            var parts = Regex.Split(section, ",|;| e ")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                p = Regex.Replace(p, "^contém[:]?\\s*", "", RegexOptions.IgnoreCase);
                p = Regex.Replace(p, "^de\\s+", "", RegexOptions.IgnoreCase);
                p = p.Trim().Trim(',', '.', ';');
                parts[i] = p;
            }

            return parts;
        }

        private static string CleanIngredient(string ingredient)
        {
            if (string.IsNullOrWhiteSpace(ingredient))
                return string.Empty;

            // Remove caracteres inválidos comuns de OCR
            var cleaned = ingredient;
            cleaned = Regex.Replace(cleaned, @"[|\\\/\[\]{}]", ""); // remove símbolos inválidos
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " "); // remove espaços múltiplos
            cleaned = cleaned.Trim();

            return cleaned;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 5: DETECTAR ALERGÊNICOS
        // ═══════════════════════════════════════════════════════════════════════════════
        private static void DetectAllergens(string text, IngredientAllergenParseResult result)
        {
            foreach (var allergen in CommonAllergens)
            {
                var pattern = "\\b" + Regex.Escape(allergen) + "\\b";
                var m = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
                if (m.Count > 0)
                {
                    result.Allergens.Add(allergen);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 6: EXTRAIR FRASES CRÍTICAS E CLASSIFICAR ALERGÊNICOS
        // ═══════════════════════════════════════════════════════════════════════════════
        private static void ExtractCriticalPhrases(string text, IngredientAllergenParseResult result)
        {
            // IMPORTANTE: Processar frases negativas PRIMEIRO para evitar falsos positivos
            // Ex: "NÃO CONTÉM GLÚTEN" não deve gerar alergênico positivo
            var negativePhrases = new[] { "não contém", "nao contem", "isento de", "sem lactose", "sem glúten", "livre de" };
            var positivePhrases = new[] { "contém", "pode conter" };

            // Rastrear alérgenos que foram explicitamente negados
            var deniedAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // PASSO 1: Identificar todos os alérgenos negados
            foreach (var term in negativePhrases)
            {
                var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                while (idx >= 0)
                {
                    var phrase = ExtractPhrase(text, idx);

                    foreach (var allergen in CommonAllergens)
                    {
                        if (Regex.IsMatch(phrase, "\\b" + Regex.Escape(allergen) + "\\b", RegexOptions.IgnoreCase))
                        {
                            deniedAllergens.Add(allergen);

                            // Adicionar à lista de frases extraídas
                            if (!result.ExtractedPhrases.Contains(phrase))
                            {
                                result.ExtractedPhrases.Add(phrase);
                            }
                            if (!result.CriticalTerms.Contains(term))
                            {
                                result.CriticalTerms.Add(term);
                            }
                        }
                    }

                    // Buscar próxima ocorrência
                    idx = text.IndexOf(term, idx + term.Length, StringComparison.OrdinalIgnoreCase);
                }
            }

            // PASSO 2: Processar frases positivas, excluindo alérgenos já negados
            foreach (var term in positivePhrases)
            {
                var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                while (idx >= 0)
                {
                    // Verificar se não está dentro de uma frase negativa
                    var contextStart = Math.Max(0, idx - 10);
                    var contextBefore = text.Substring(contextStart, idx - contextStart).ToLowerInvariant();

                    if (contextBefore.Contains("não") || contextBefore.Contains("nao"))
                    {
                        // Esta é uma frase negativa, pular
                        idx = text.IndexOf(term, idx + term.Length, StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    var phrase = ExtractPhrase(text, idx);
                    result.ExtractedPhrases.Add(phrase);

                    // Classificar alergênicos com base no contexto
                    bool isConfirmed = term.Equals("contém", StringComparison.OrdinalIgnoreCase);
                    bool isPotential = term.Equals("pode conter", StringComparison.OrdinalIgnoreCase);

                    foreach (var allergen in CommonAllergens)
                    {
                        if (Regex.IsMatch(phrase, "\\b" + Regex.Escape(allergen) + "\\b", RegexOptions.IgnoreCase))
                        {
                            // NÃO ADICIONAR se foi explicitamente negado
                            if (deniedAllergens.Contains(allergen))
                            {
                                continue;
                            }

                            if (!result.Allergens.Contains(allergen))
                            {
                                result.Allergens.Add(allergen);
                            }

                            // Adicionar às listas específicas
                            if (isConfirmed && !result.ConfirmedAllergens.Contains(allergen))
                            {
                                result.ConfirmedAllergens.Add(allergen);
                            }
                            else if (isPotential && !result.MayContainAllergens.Contains(allergen))
                            {
                                result.MayContainAllergens.Add(allergen);
                            }
                        }
                    }

                    result.CriticalTerms.Add(term);

                    idx = text.IndexOf(term, idx + term.Length, StringComparison.OrdinalIgnoreCase);
                }
            }

            // PASSO 3: Remover da lista geral de Allergens os que foram explicitamente negados
            result.Allergens = result.Allergens
                .Where(a => !deniedAllergens.Contains(a))
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ETAPA 7: VALIDAÇÃO FINAL E AJUSTE DE CONFIANÇA
        // ═══════════════════════════════════════════════════════════════════════════════
        private static void FinalValidationAndConfidenceAdjustment(IngredientAllergenParseResult result, string rawText)
        {
            var warningCount = result.ValidationWarnings.Count;
            var confidenceScore = 100;

            // Reduzir confiança se nome do produto inválido ou não encontrado
            if (string.IsNullOrWhiteSpace(result.ProductName) || !result.IsProductNameValidated)
            {
                confidenceScore -= 30;
                if (string.IsNullOrWhiteSpace(result.ProductName))
                {
                    result.ValidationWarnings.Add("Nome do produto não identificado");
                }
            }

            // Reduzir confiança se marca não encontrada (menos crítico)
            if (string.IsNullOrWhiteSpace(result.Brand))
            {
                confidenceScore -= 10;
            }

            // Reduzir confiança se ingredientes não encontrados
            if (!result.HasIngredients)
            {
                confidenceScore -= 20;
                result.ValidationWarnings.Add("Nenhum ingrediente identificado");
            }

            // Reduzir confiança se texto contém muito ruído
            var noiseLevel = CalculateNoiseLevel(rawText);
            if (noiseLevel > 0.3)
            {
                confidenceScore -= 20;
                result.ValidationWarnings.Add($"Texto com alto nível de ruído ({noiseLevel:P0})");
            }

            // Ajustar confiança baseado em parsing incompleto
            if (warningCount > 3)
            {
                confidenceScore -= 15;
            }

            // Mapear score para ConfidenceLevel
            result.ParsingConfidence = confidenceScore switch
            {
                >= 80 => ConfidenceLevel.High,
                >= 50 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        private static double CalculateNoiseLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 1.0;

            var totalChars = text.Length;
            var invalidChars = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsPunctuation(c));

            return (double)invalidChars / totalChars;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // MÉTODOS AUXILIARES
        // ═══════════════════════════════════════════════════════════════════════════════
        private static NutritionData? ExtractNutritionInfo(string text)
        {
            var nutrition = new NutritionData();
            bool hasData = false;

            var servingMatch = Regex.Match(text, @"porção\s*[:\-]?\s*(\d+\s*g|[\d,]+\s*ml|[\d/]+\s*xícara|\d+\s*g\s*\([^)]+\))", RegexOptions.IgnoreCase);
            if (servingMatch.Success)
            {
                nutrition.ServingSize = servingMatch.Groups[1].Value.Trim();
                hasData = true;
            }

            nutrition.Calories = ExtractNutrientValue(text, new[] { "valor energético", "calorias", "energia" }, "kcal");
            if (nutrition.Calories.HasValue) hasData = true;

            nutrition.TotalCarbohydrate = ExtractNutrientValue(text, new[] { "carboidratos", "carbohidratos" }, "g");
            if (nutrition.TotalCarbohydrate.HasValue) hasData = true;

            nutrition.Protein = ExtractNutrientValue(text, new[] { "proteínas", "proteinas" }, "g");
            if (nutrition.Protein.HasValue) hasData = true;

            nutrition.TotalFat = ExtractNutrientValue(text, new[] { "gorduras totais", "lipídios" }, "g");
            if (nutrition.TotalFat.HasValue) hasData = true;

            nutrition.SaturatedFat = ExtractNutrientValue(text, new[] { "gorduras saturadas", "saturadas" }, "g");
            if (nutrition.SaturatedFat.HasValue) hasData = true;

            nutrition.TransFat = ExtractNutrientValue(text, new[] { "gorduras trans", "trans" }, "g");
            if (nutrition.TransFat.HasValue) hasData = true;

            nutrition.DietaryFiber = ExtractNutrientValue(text, new[] { "fibra alimentar", "fibras" }, "g");
            if (nutrition.DietaryFiber.HasValue) hasData = true;

            nutrition.Sodium = ExtractNutrientValue(text, new[] { "sódio", "sodio" }, "mg");
            if (nutrition.Sodium.HasValue) hasData = true;

            nutrition.Sugars = ExtractNutrientValue(text, new[] { "açúcares", "acucares" }, "g");
            if (nutrition.Sugars.HasValue) hasData = true;

            // Nutrientes de suplementos
            nutrition.Creatine = ExtractNutrientValue(text, new[] { "creatina", "creatine" }, "mg");
            if (!nutrition.Creatine.HasValue)
            {
                // Tentar em gramas e converter para mg
                var creatineInGrams = ExtractNutrientValue(text, new[] { "creatina", "creatine" }, "g");
                if (creatineInGrams.HasValue)
                {
                    nutrition.Creatine = creatineInGrams.Value * 1000; // converter g para mg
                }
            }
            if (nutrition.Creatine.HasValue) hasData = true;

            nutrition.Caffeine = ExtractNutrientValue(text, new[] { "cafeína", "cafeina", "caffeine" }, "mg");
            if (nutrition.Caffeine.HasValue) hasData = true;

            nutrition.Bcaa = ExtractNutrientValue(text, new[] { "bcaa", "aminoácidos" }, "mg");
            if (!nutrition.Bcaa.HasValue)
            {
                var bcaaInGrams = ExtractNutrientValue(text, new[] { "bcaa", "aminoácidos" }, "g");
                if (bcaaInGrams.HasValue)
                {
                    nutrition.Bcaa = bcaaInGrams.Value * 1000;
                }
            }
            if (nutrition.Bcaa.HasValue) hasData = true;

            return hasData ? nutrition : null;
        }

        private static double? ExtractNutrientValue(string text, string[] nutrientNames, string unit)
        {
            foreach (var name in nutrientNames)
            {
                var pattern = Regex.Escape(name) + @"\s*[:\-]?\s*([\d,]+)\s*" + Regex.Escape(unit);
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var valueStr = match.Groups[1].Value.Replace(",", ".");
                    if (double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        return value;
                    }
                }
            }
            return null;
        }

        private static string Normalize(string s)
        {
            var t = s ?? string.Empty;
            t = t.Replace('\r', ' ').Replace('\n', ' ');
            t = Regex.Replace(t, @"[ \u00A0\t]+", " ");
            t = Regex.Replace(t, @"[–—]", "-");
            t = Regex.Replace(t, "[.]{2,}", ".");
            t = t.ToLowerInvariant();
            t = Regex.Replace(t, @"\s{2,}", " ");
            return t.Trim();
        }

        private static int IndexOfAny(string text, string[] needles, int start)
        {
            var min = -1;
            foreach (var n in needles)
            {
                var i = text.IndexOf(n, start, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && (min == -1 || i < min)) min = i;
            }
            return min;
        }

        private static string ExtractPhrase(string text, int idx)
        {
            var end = text.IndexOf('.', idx);
            if (end < 0) end = Math.Min(text.Length, idx + 120);
            var phrase = text.Substring(idx, Math.Min(end - idx, 200));
            return phrase.Trim();
        }

        /// <summary>
        /// Remove ruído de OCR: aspas, barras invertidas, caracteres especiais nas bordas.
        /// </summary>
        private static string CleanOcrNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = text;

            // Remove aspas de qualquer tipo no início e fim
            cleaned = Regex.Replace(cleaned, @"^[\""'\'\""\""`´\\]+", "");
            cleaned = Regex.Replace(cleaned, @"[\""'\'\""\""`´\\]+$", "");

            // Remove barras invertidas
            cleaned = cleaned.Replace("\\", "");

            // Remove caracteres especiais isolados no início/fim
            cleaned = Regex.Replace(cleaned, @"^[\*\#\@\!\?\$\%\^\&\(\)\[\]\{\}\|\/\<\>]+", "");
            cleaned = Regex.Replace(cleaned, @"[\*\#\@\!\?\$\%\^\&\(\)\[\]\{\}\|\/\<\>]+$", "");

            // Remove espaços extras
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

            // Trim final
            cleaned = cleaned.Trim();

            return cleaned;
        }

        /// <summary>
        /// Verifica se o texto é uma keyword inválida para marca.
        /// </summary>
        private static bool IsInvalidBrandKeyword(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            var normalized = text.ToUpperInvariant();

            var invalidBrandKeywords = new[]
            {
                "INFORMAÇÃO NUTRICIONAL", "INFORMACAO NUTRICIONAL",
                "TABELA NUTRICIONAL", "VALORES NUTRICIONAIS",
                "INGREDIENTES", "CONTÉM", "CONTEM",
                "PORÇÃO", "PORCAO", "PORÇÕES", "PORCOES",
                "VALOR ENERGÉTICO", "VALOR ENERGETICO",
                "CARBOIDRATO", "PROTEÍNA", "PROTEINA", "GORDURA",
                "SÓDIO", "SODIO", "FIBRA",
                "%VD", "% VD", "VD%",
                "KCAL", "CALORIAS"
            };

            return invalidBrandKeywords.Any(kw => normalized.Contains(kw));
        }
    }
}
