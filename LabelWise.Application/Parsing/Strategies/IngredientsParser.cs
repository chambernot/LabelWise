using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Implementação do parser de lista de ingredientes.
    /// Extrai ingredientes após "INGREDIENTES", limpa ruídos de OCR, normaliza delimitadores.
    /// </summary>
    public class IngredientsParser : IIngredientsParser
    {
        private static readonly string[] IngredientsSectionKeywords = 
        {
            "ingredientes:",
            "ingredientes -",
            "ingredientes",
            "lista de ingredientes:",
            "lista de ingredientes"
        };

        private static readonly string[] SectionEndKeywords =
        {
            "informação nutricional",
            "informacao nutricional",
            "tabela nutricional",
            "valores nutricionais",
            "alérgicos",
            "alergicos",
            "contém:",
            "pode conter:",
            "não contém:",
            "nao contem:"
        };

        public IngredientsParseResult Parse(string ocrText)
        {
            var result = new IngredientsParseResult
            {
                Confidence = ConfidenceLevel.High
            };

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Texto OCR vazio ou nulo");
                return result;
            }

            var normalizedText = NormalizeText(ocrText);

            // Extrair seção de ingredientes
            var ingredientsSection = ExtractIngredientsSection(normalizedText);

            if (string.IsNullOrWhiteSpace(ingredientsSection))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Seção de ingredientes não encontrada");
                return result;
            }

            result.RawIngredientsSection = ingredientsSection;

            // Dividir e limpar ingredientes
            var ingredients = SplitAndCleanIngredients(ingredientsSection);
            result.Ingredients = ingredients;

            // Validação e ajuste de confiança
            ValidateAndAdjustConfidence(result);

            return result;
        }

        private static string NormalizeText(string text)
        {
            var normalized = text ?? string.Empty;
            normalized = normalized.Replace('\r', ' ').Replace('\n', ' ');
            normalized = Regex.Replace(normalized, @"[ \u00A0\t]+", " ");
            normalized = Regex.Replace(normalized, @"[–—]", "-");
            normalized = normalized.ToLowerInvariant();
            return normalized.Trim();
        }

        private static string ExtractIngredientsSection(string text)
        {
            // Tentar encontrar início da seção de ingredientes
            int startIndex = -1;
            string? matchedKeyword = null;

            foreach (var keyword in IngredientsSectionKeywords)
            {
                var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (startIndex == -1 || index < startIndex))
                {
                    startIndex = index;
                    matchedKeyword = keyword;
                }
            }

            if (startIndex == -1)
            {
                return string.Empty;
            }

            // Avançar após a keyword
            startIndex += matchedKeyword!.Length;

            // Encontrar fim da seção
            int endIndex = text.Length;

            foreach (var endKeyword in SectionEndKeywords)
            {
                var index = text.IndexOf(endKeyword, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && index < endIndex)
                {
                    endIndex = index;
                }
            }

            // Extrair seção
            var section = text.Substring(startIndex, endIndex - startIndex);
            return section.Trim();
        }

        private static List<string> SplitAndCleanIngredients(string section)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                return new List<string>();
            }

            // Remover prefixos comuns de OCR
            section = Regex.Replace(section, @"^(contém[:]?\s*|de\s+)", "", RegexOptions.IgnoreCase);

            // Dividir por vírgula, ponto e vírgula, " e ", " ou "
            var delimiters = new[] { ',', ';' };
            var parts = section.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(p => Regex.Split(p, @"\s+e\s+", RegexOptions.IgnoreCase))
                .SelectMany(p => Regex.Split(p, @"\s+ou\s+", RegexOptions.IgnoreCase))
                .Select(p => CleanIngredient(p))
                .Where(p => !string.IsNullOrWhiteSpace(p) && IsValidIngredient(p))
                .Distinct()
                .ToList();

            return parts;
        }

        private static string CleanIngredient(string ingredient)
        {
            if (string.IsNullOrWhiteSpace(ingredient))
            {
                return string.Empty;
            }

            var cleaned = ingredient.Trim();

            // Remover caracteres inválidos comuns de OCR
            cleaned = Regex.Replace(cleaned, @"[|\\/\[\]{}]", "");
            cleaned = Regex.Replace(cleaned, @"[<>«»]", "");
            cleaned = Regex.Replace(cleaned, @"[""\u201C\u201D]", ""); // Aspas tipográficas
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

            // Remover pontuação no início e fim
            cleaned = cleaned.Trim('.', ',', ';', ':', '-', '_', '*', '+', '=');

            // Remover espaços
            cleaned = cleaned.Trim();

            return cleaned;
        }

        private static bool IsValidIngredient(string ingredient)
        {
            if (string.IsNullOrWhiteSpace(ingredient))
            {
                return false;
            }

            // Deve ter pelo menos 2 caracteres
            if (ingredient.Length < 2)
            {
                return false;
            }

            // Deve conter pelo menos uma letra
            if (!ingredient.Any(char.IsLetter))
            {
                return false;
            }

            // Não pode ser apenas números
            if (ingredient.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
            {
                return false;
            }

            // Não pode ter mais de 80% de caracteres especiais
            var specialChars = ingredient.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
            if (specialChars > ingredient.Length * 0.8)
            {
                return false;
            }

            // Filtrar palavras-chave inválidas
            var invalidKeywords = new[]
            {
                "informação nutricional",
                "informacao nutricional",
                "tabela nutricional",
                "valores nutricionais",
                "% vd",
                "%vd",
                "kcal",
                "porção",
                "porcao"
            };

            var normalizedIngredient = ingredient.ToLowerInvariant();
            if (invalidKeywords.Any(kw => normalizedIngredient.Contains(kw)))
            {
                return false;
            }

            return true;
        }

        private static void ValidateAndAdjustConfidence(IngredientsParseResult result)
        {
            if (!result.HasIngredients)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nenhum ingrediente válido encontrado");
                return;
            }

            // Validar quantidade mínima
            if (result.Ingredients.Count < 2)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add($"Apenas {result.Ingredients.Count} ingrediente encontrado (esperado 2+)");
            }
            else if (result.Ingredients.Count < 3)
            {
                result.Confidence = ConfidenceLevel.Medium;
                result.ValidationWarnings.Add($"{result.Ingredients.Count} ingredientes encontrados (esperado 3+)");
            }

            // Validar comprimento médio dos ingredientes
            var avgLength = result.Ingredients.Average(i => i.Length);
            if (avgLength < 3)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add($"Comprimento médio dos ingredientes muito baixo ({avgLength:F1} caracteres)");
            }
        }
    }
}
