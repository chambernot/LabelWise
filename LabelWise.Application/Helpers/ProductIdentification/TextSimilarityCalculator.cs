namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Helper para cálculo de similaridade de texto (fuzzy matching).
    /// Utilizado para encontrar produtos candidatos quando a identificação exata falha.
    /// </summary>
    public static class TextSimilarityCalculator
    {
        /// <summary>
        /// Calcula a distância de Levenshtein entre duas strings.
        /// </summary>
        public static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target))
                return source.Length;

            var sourceLength = source.Length;
            var targetLength = target.Length;

            var matrix = new int[sourceLength + 1, targetLength + 1];

            for (var i = 0; i <= sourceLength; i++)
                matrix[i, 0] = i;
            for (var j = 0; j <= targetLength; j++)
                matrix[0, j] = j;

            for (var i = 1; i <= sourceLength; i++)
            {
                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[sourceLength, targetLength];
        }

        /// <summary>
        /// Calcula similaridade normalizada entre duas strings (0.0 a 1.0).
        /// Baseado na distância de Levenshtein normalizada.
        /// </summary>
        public static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                return 0.0;

            // Normaliza strings
            source = NormalizeText(source);
            target = NormalizeText(target);

            if (source == target)
                return 1.0;

            var distance = LevenshteinDistance(source, target);
            var maxLength = Math.Max(source.Length, target.Length);

            return Math.Round(1.0 - (double)distance / maxLength, 4);
        }

        /// <summary>
        /// Calcula similaridade baseada em tokens (palavras comuns).
        /// Útil para nomes de produtos que podem estar em ordens diferentes.
        /// </summary>
        public static double CalculateTokenSimilarity(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                return 0.0;

            var sourceTokens = TokenizeText(source);
            var targetTokens = TokenizeText(target);

            if (sourceTokens.Count == 0 || targetTokens.Count == 0)
                return 0.0;

            var intersection = sourceTokens.Intersect(targetTokens).Count();
            var union = sourceTokens.Union(targetTokens).Count();

            // Coeficiente de Jaccard
            return Math.Round((double)intersection / union, 4);
        }

        /// <summary>
        /// Calcula similaridade combinada (Levenshtein + Tokens).
        /// </summary>
        public static double CalculateCombinedSimilarity(string source, string target)
        {
            var levenshteinSim = CalculateSimilarity(source, target);
            var tokenSim = CalculateTokenSimilarity(source, target);

            // Peso maior para similaridade de tokens em textos longos
            var sourceLength = source?.Length ?? 0;
            var tokenWeight = Math.Min(0.7, 0.3 + sourceLength * 0.01);

            return Math.Round(
                levenshteinSim * (1 - tokenWeight) + tokenSim * tokenWeight, 
                4);
        }

        /// <summary>
        /// Verifica se duas strings são similares acima de um threshold.
        /// </summary>
        public static bool IsSimilar(string source, string target, double threshold = 0.6)
        {
            return CalculateCombinedSimilarity(source, target) >= threshold;
        }

        /// <summary>
        /// Encontra a melhor correspondência para um texto em uma lista.
        /// </summary>
        public static (string? BestMatch, double Similarity) FindBestMatch(
            string query, 
            IEnumerable<string> candidates)
        {
            string? bestMatch = null;
            double bestSimilarity = 0.0;

            foreach (var candidate in candidates)
            {
                var similarity = CalculateCombinedSimilarity(query, candidate);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = candidate;
                }
            }

            return (bestMatch, bestSimilarity);
        }

        /// <summary>
        /// Encontra todas as correspondências acima de um threshold.
        /// </summary>
        public static List<(string Match, double Similarity)> FindMatches(
            string query,
            IEnumerable<string> candidates,
            double threshold = 0.4,
            int maxResults = 10)
        {
            return candidates
                .Select(c => (Match: c, Similarity: CalculateCombinedSimilarity(query, c)))
                .Where(x => x.Similarity >= threshold)
                .OrderByDescending(x => x.Similarity)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Calcula similaridade entre duas listas de strings (ex: ingredientes).
        /// </summary>
        public static double CalculateListSimilarity(
            IEnumerable<string> source, 
            IEnumerable<string> target,
            double tokenThreshold = 0.7)
        {
            var sourceList = source.Select(NormalizeText).ToList();
            var targetList = target.Select(NormalizeText).ToList();

            if (sourceList.Count == 0 || targetList.Count == 0)
                return 0.0;

            var matchCount = 0;
            foreach (var sourceItem in sourceList)
            {
                foreach (var targetItem in targetList)
                {
                    if (CalculateSimilarity(sourceItem, targetItem) >= tokenThreshold)
                    {
                        matchCount++;
                        break;
                    }
                }
            }

            // Proporção de itens da source que têm match na target
            return Math.Round((double)matchCount / sourceList.Count, 4);
        }

        /// <summary>
        /// Normaliza texto para comparação (lowercase, remove acentos, etc.).
        /// </summary>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Lowercase
            text = text.ToLowerInvariant();

            // Remove acentos
            text = RemoveDiacritics(text);

            // Remove caracteres especiais exceto espaços
            text = new string(text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());

            // Normaliza espaços múltiplos
            text = string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            return text.Trim();
        }

        /// <summary>
        /// Remove acentos e diacríticos.
        /// </summary>
        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        /// <summary>
        /// Tokeniza texto em palavras significativas.
        /// </summary>
        private static HashSet<string> TokenizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            var normalized = NormalizeText(text);
            var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Filtra stopwords e tokens muito curtos
            var stopwords = new HashSet<string> 
            { 
                "de", "da", "do", "das", "dos", "e", "em", "um", "uma", 
                "com", "para", "por", "ao", "aos", "a", "o", "as", "os",
                "the", "and", "or", "of", "to", "in", "on", "at", "for"
            };

            return tokens
                .Where(t => t.Length >= 2 && !stopwords.Contains(t))
                .ToHashSet();
        }
    }
}
