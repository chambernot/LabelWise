using LabelWise.Domain.Enums;

namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Helper para cálculo de confiança de matching e classificação de resultados.
    /// </summary>
    public static class MatchConfidenceCalculator
    {
        /// <summary>
        /// Calcula a confiança do match baseado na fonte e qualidade dos dados.
        /// </summary>
        public static double CalculateMatchConfidence(
            MatchSource matchSource,
            bool isFromExternalDatabase,
            double? ocrConfidence = null,
            bool hasMultipleSources = false)
        {
            double baseConfidence = matchSource switch
            {
                MatchSource.Barcode => isFromExternalDatabase ? 0.95 : 0.85,
                MatchSource.FrontOcr => ocrConfidence ?? 0.60,
                MatchSource.Similarity => 0.70,
                MatchSource.Combined => 0.90,
                MatchSource.Unknown => 0.20,
                _ => 0.50
            };

            // Boost de confiança se houver múltiplas fontes confirmando
            if (hasMultipleSources)
            {
                baseConfidence = Math.Min(1.0, baseConfidence * 1.15);
            }

            // Boost se veio de base externa confiável
            if (isFromExternalDatabase && matchSource != MatchSource.Unknown)
            {
                baseConfidence = Math.Min(1.0, baseConfidence * 1.05);
            }

            return Math.Round(baseConfidence, 4);
        }

        /// <summary>
        /// Determina se o match é confiável o suficiente para prosseguir automaticamente.
        /// </summary>
        public static bool IsReliableMatch(double matchConfidence, MatchSource matchSource)
        {
            // Barcode com confiança alta é sempre confiável
            if (matchSource == MatchSource.Barcode && matchConfidence >= 0.85)
                return true;

            // Combined (múltiplas fontes) com confiança alta
            if (matchSource == MatchSource.Combined && matchConfidence >= 0.85)
                return true;

            // Outros métodos precisam de confiança muito alta
            if (matchConfidence >= 0.90)
                return true;

            return false;
        }

        /// <summary>
        /// Classifica o nível de confiança em categorias legíveis.
        /// </summary>
        public static string GetConfidenceLabel(double confidence)
        {
            return confidence switch
            {
                >= 0.90 => "Muito Alta",
                >= 0.75 => "Alta",
                >= 0.60 => "Média",
                >= 0.40 => "Baixa",
                _ => "Muito Baixa"
            };
        }

        /// <summary>
        /// Calcula score de similaridade entre dois textos (nome/marca).
        /// Algoritmo simplificado: Levenshtein distance normalizado.
        /// </summary>
        public static double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return 0.0;

            text1 = text1.Trim().ToLowerInvariant();
            text2 = text2.Trim().ToLowerInvariant();

            if (text1 == text2)
                return 1.0;

            int distance = LevenshteinDistance(text1, text2);
            int maxLength = Math.Max(text1.Length, text2.Length);

            double similarity = 1.0 - ((double)distance / maxLength);
            return Math.Round(similarity, 4);
        }

        /// <summary>
        /// Calcula a distância de Levenshtein entre duas strings.
        /// </summary>
        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[s1.Length, s2.Length];
        }

        /// <summary>
        /// Valida se um nome de produto extraído por OCR parece válido.
        /// </summary>
        public static bool IsValidProductName(string? productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return false;

            productName = productName.Trim();

            // Muito curto
            if (productName.Length < 3)
                return false;

            // Muito longo (provavelmente pegou texto indesejado)
            if (productName.Length > 100)
                return false;

            // Deve ter pelo menos uma letra
            if (!productName.Any(char.IsLetter))
                return false;

            // Não deve ser apenas números
            if (productName.All(char.IsDigit))
                return false;

            return true;
        }

        /// <summary>
        /// Valida se uma marca extraída por OCR parece válida.
        /// </summary>
        public static bool IsValidBrand(string? brand)
        {
            if (string.IsNullOrWhiteSpace(brand))
                return false;

            brand = brand.Trim();

            // Muito curto
            if (brand.Length < 2)
                return false;

            // Muito longo
            if (brand.Length > 50)
                return false;

            // Deve ter pelo menos uma letra
            if (!brand.Any(char.IsLetter))
                return false;

            return true;
        }
    }
}
