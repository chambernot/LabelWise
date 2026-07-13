using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Implementação do parser da frente da embalagem.
    /// Extrai nome do produto e marca. Ignora linhas que pareçam tabela nutricional.
    /// </summary>
    public class FrontPackagingParser : IFrontPackagingParser
    {
        private static readonly string[] NutritionalTableKeywords =
        {
            "informação nutricional", "informacao nutricional", "nutrition facts",
            "tabela nutricional", "valores nutricionais",
            "%vd", "% vd", "vd%",
            "kcal", "calorias", "valor energético", "valor energetico", "energy",
            "porção", "porcao", "serving size", "amount per serving",
            "carboidrato", "carbohydrate",
            "proteína", "proteina", "protein",
            "gordura", "lipídio", "lipidio", "fat",
            "sódio", "sodio", "sodium",
            "fibra alimentar", "dietary fiber",
            "saturada", "saturates", "saturated",
            "trans"
        };

        private static readonly string[] InvalidKeywords =
        {
            "ingredientes", "contém", "contem", "pode conter", "não contém", "nao contem",
            "validade", "peso líquido", "peso liquido", "cont.", "conteúdo", "conteudo",
            "conservar", "lote", "fabricação", "fabricacao",
            "sac", "cnpj", "indústria", "industria", "ltda", "s.a.", "s/a",
            "rua", "avenida", "av.", "telefone", "tel.", "fone",
            "www", "http", "@", ".com", ".br"
        };

        private static readonly string[] FlavorKeywords =
        {
            "sabor", "flavour", "flavor", "gosto"
        };

        public FrontPackagingParseResult Parse(string ocrText)
        {
            var result = new FrontPackagingParseResult
            {
                Confidence = ConfidenceLevel.High
            };

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Texto OCR vazio ou nulo");
                return result;
            }

            // Dividir em linhas e filtrar
            var lines = SplitAndFilterLines(ocrText);

            if (lines.Count == 0)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nenhuma linha válida encontrada");
                return result;
            }

            // Extrair informações do produto
            ExtractProductInfo(lines, result);

            // Validação e ajuste de confiança
            ValidateAndAdjustConfidence(result);

            return result;
        }

        private static List<string> SplitAndFilterLines(string text)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && IsValidProductLine(l))
                .Take(20) // Limitar às primeiras 20 linhas válidas
                .ToList();

            return lines;
        }

        private static bool IsValidProductLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var normalized = line.ToLowerInvariant();

            // Filtrar linhas de tabela nutricional
            if (NutritionalTableKeywords.Any(kw => normalized.Contains(kw)))
            {
                return false;
            }

            // Filtrar linhas com keywords inválidas
            if (InvalidKeywords.Any(kw => normalized.Contains(kw)))
            {
                return false;
            }

            // Filtrar linhas que são apenas números
            if (Regex.IsMatch(line, @"^\d+$"))
            {
                return false;
            }

            // Filtrar linhas com muitos números (provavelmente tabela)
            var digitCount = line.Count(char.IsDigit);
            var digitPercentage = (double)digitCount / line.Length;
            if (digitPercentage > 0.6)
            {
                return false;
            }

            // Filtrar linhas com muitos símbolos especiais
            var specialChars = line.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
            if (specialChars > line.Length / 3)
            {
                return false;
            }

            // Deve conter pelo menos uma letra
            if (!line.Any(char.IsLetter))
            {
                return false;
            }

            // Tamanho mínimo
            if (line.Length < 2)
            {
                return false;
            }

            // Tamanho máximo razoável
            if (line.Length > 100)
            {
                return false;
            }

            return true;
        }

        private static void ExtractProductInfo(List<string> lines, FrontPackagingParseResult result)
        {
            // A primeira linha válida geralmente é o nome do produto
            if (lines.Count > 0)
            {
                var cleanedName = CleanOcrNoise(lines[0]);
                if (!string.IsNullOrWhiteSpace(cleanedName))
                {
                    result.ProductName = cleanedName;
                    result.IsProductNameValidated = true;
                }
            }

            // A segunda linha pode ser a marca ou complemento do nome
            if (lines.Count > 1)
            {
                var secondLine = CleanOcrNoise(lines[1]);

                // Verificar se é um sabor
                if (IsFlavor(secondLine))
                {
                    result.Flavor = secondLine;
                }
                // Caso contrário, pode ser a marca
                else if (IsPotentialBrand(secondLine))
                {
                    result.Brand = secondLine;
                    result.IsBrandValidated = true;
                }
            }

            // Procurar marca nas linhas restantes se ainda não foi encontrada
            if (string.IsNullOrWhiteSpace(result.Brand) && lines.Count > 2)
            {
                for (int i = 2; i < Math.Min(lines.Count, 5); i++)
                {
                    var cleanedLine = CleanOcrNoise(lines[i]);
                    if (IsPotentialBrand(cleanedLine))
                    {
                        result.Brand = cleanedLine;
                        result.IsBrandValidated = true;
                        break;
                    }
                }
            }

            // Procurar sabor nas linhas se ainda não foi encontrado
            if (string.IsNullOrWhiteSpace(result.Flavor) && lines.Count > 1)
            {
                for (int i = 1; i < Math.Min(lines.Count, 5); i++)
                {
                    var cleanedLine = CleanOcrNoise(lines[i]);
                    if (IsFlavor(cleanedLine))
                    {
                        result.Flavor = cleanedLine;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Remove ruído de OCR: aspas, barras invertidas, caracteres especiais nas bordas.
        /// </summary>
        private static string CleanOcrNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = text;

            // Remove common OCR noise characters like backslashes or pipes that can be anywhere.
            cleaned = cleaned.Replace("\\", "").Replace("|", "");

            // Remove quotes and apostrophes from start and end. Handles ' " ` ´ ’
            // Also trims whitespace before and after.
            cleaned = Regex.Replace(cleaned, @"^[\s""'´`’]+|[\s""'´`’]+$", "");

            // Remove any remaining non-word characters from the very beginning or end.
            cleaned = Regex.Replace(cleaned, @"^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$", "");

            // Replace multiple spaces with a single space
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

            // Specific case: if the result is just a single non-letter/digit character
            if (cleaned.Length == 1 && !char.IsLetterOrDigit(cleaned[0]))
            {
                return string.Empty;
            }

            return cleaned;
        }

        private static bool IsFlavor(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
            var normalized = line.ToLowerInvariant();
            return FlavorKeywords.Any(kw => normalized.Contains(kw));
        }

        private static bool IsPotentialBrand(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var normalized = line.ToLowerInvariant();

            // Stricter check for nutritional keywords.
            if (NutritionalTableKeywords.Any(kw => normalized.Contains(kw)))
            {
                return false;
            }

            // Marcas are generally short.
            if (line.Length < 2 || line.Length > 25)
            {
                return false;
            }

            // Marcas tend to have the first letter capitalized.
            if (char.IsLower(line[0]))
            {
                return false;
            }

            // Should not have too many numbers.
            var digitCount = line.Count(char.IsDigit);
            if (digitCount > line.Length / 3)
            {
                return false;
            }

            // Filter out all-caps lines that look like headers.
            if (line.Length > 10 && line.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                return false;
            }

            return true;
        }

        private static void ValidateAndAdjustConfidence(FrontPackagingParseResult result)
        {
            if (!result.HasProductInfo)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nenhuma informação de produto encontrada");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.ProductName))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nome do produto não identificado");
            }
            else if (result.ProductName.Length < 3)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nome do produto muito curto (possível ruído)");
            }

            if (string.IsNullOrWhiteSpace(result.Brand))
            {
                result.Confidence = ConfidenceLevel.Medium;
                result.ValidationWarnings.Add("Marca não identificada");
            }
        }
    }
}
