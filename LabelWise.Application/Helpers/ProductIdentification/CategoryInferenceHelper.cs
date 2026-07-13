namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Helper para inferência de categoria de produtos baseada em texto e ingredientes.
    /// Usado para melhorar a busca de candidatos quando a identificação falha.
    /// </summary>
    public static class CategoryInferenceHelper
    {
        /// <summary>
        /// Mapeamento de palavras-chave para categorias.
        /// </summary>
        private static readonly Dictionary<string, List<string>> CategoryKeywords = new()
        {
            ["Bebida"] = [
                "água", "suco", "refrigerante", "água gaseificada", "energético",
                "chá", "café", "leite", "iogurte líquido", "smoothie", "shake",
                "cerveja", "vinho", "destilado", "bebida", "drink", "juice",
                "soda", "water", "tea", "coffee", "milk"
            ],
            ["Laticínio"] = [
                "leite", "queijo", "iogurte", "manteiga", "requeijão", "creme",
                "nata", "coalhada", "ricota", "mussarela", "parmesão", "cream cheese",
                "dairy", "cheese", "yogurt", "butter", "milk", "cream"
            ],
            ["Snack"] = [
                "biscoito", "bolacha", "salgadinho", "chips", "batata", "amendoim",
                "castanha", "barra", "wafer", "cracker", "cookie", "snack",
                "pretzel", "pipoca", "popcorn", "nut", "granola"
            ],
            ["Cereal"] = [
                "cereal", "aveia", "granola", "flocos", "müsli", "corn flakes",
                "oat", "wheat", "rice", "bran", "fiber"
            ],
            ["Pão e Padaria"] = [
                "pão", "bolo", "torta", "massa", "croissant", "brioche",
                "bread", "cake", "pastry", "muffin", "donut"
            ],
            ["Carne e Proteína"] = [
                "carne", "frango", "peixe", "atum", "sardinha", "salsicha",
                "presunto", "bacon", "linguiça", "hambúrguer", "protein",
                "meat", "chicken", "fish", "tuna", "ham", "sausage"
            ],
            ["Doce e Confeitaria"] = [
                "chocolate", "bombom", "bala", "chiclete", "sorvete", "gelado",
                "pudim", "mousse", "caramelo", "açúcar", "candy", "sweet",
                "ice cream", "dessert", "confectionery"
            ],
            ["Molho e Condimento"] = [
                "molho", "ketchup", "mostarda", "maionese", "tempero", "sal",
                "pimenta", "vinagre", "azeite", "óleo", "sauce", "dressing",
                "mayonnaise", "mustard", "spice", "seasoning"
            ],
            ["Congelado"] = [
                "congelado", "frozen", "pizza", "lasanha", "nuggets", "empanado",
                "sorvete", "picolé", "ice cream"
            ],
            ["Conserva e Enlatado"] = [
                "conserva", "enlatado", "lata", "molho de tomate", "extrato",
                "ervilha", "milho", "palmito", "azeitona", "canned", "preserved"
            ],
            ["Vegetal e Fruta"] = [
                "fruta", "vegetal", "legume", "verdura", "salada", "orgânico",
                "fruit", "vegetable", "organic", "natural"
            ],
            ["Suplemento"] = [
                "whey", "proteína", "suplemento", "vitamina", "mineral",
                "creatina", "bcaa", "pré-treino", "protein", "supplement"
            ]
        };

        /// <summary>
        /// Infere a categoria do produto baseada no texto fornecido.
        /// </summary>
        public static string? InferCategory(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var normalizedText = TextSimilarityCalculator.NormalizeText(text);
            
            var scores = new Dictionary<string, int>();

            foreach (var (category, keywords) in CategoryKeywords)
            {
                var score = keywords.Count(keyword => 
                    normalizedText.Contains(TextSimilarityCalculator.NormalizeText(keyword)));
                
                if (score > 0)
                    scores[category] = score;
            }

            return scores.Count > 0
                ? scores.OrderByDescending(x => x.Value).First().Key
                : null;
        }

        /// <summary>
        /// Infere a categoria baseada em ingredientes.
        /// </summary>
        public static string? InferCategoryFromIngredients(IEnumerable<string> ingredients)
        {
            if (ingredients == null || !ingredients.Any())
                return null;

            var combinedText = string.Join(" ", ingredients);
            return InferCategory(combinedText);
        }

        /// <summary>
        /// Calcula a probabilidade de cada categoria para um texto.
        /// </summary>
        public static List<(string Category, double Probability)> GetCategoryProbabilities(
            string? text,
            IEnumerable<string>? ingredients = null)
        {
            var combinedText = text ?? "";
            if (ingredients != null)
                combinedText += " " + string.Join(" ", ingredients);

            if (string.IsNullOrWhiteSpace(combinedText))
                return [];

            var normalizedText = TextSimilarityCalculator.NormalizeText(combinedText);
            var scores = new Dictionary<string, int>();
            var totalMatches = 0;

            foreach (var (category, keywords) in CategoryKeywords)
            {
                var score = keywords.Count(keyword =>
                    normalizedText.Contains(TextSimilarityCalculator.NormalizeText(keyword)));

                if (score > 0)
                {
                    scores[category] = score;
                    totalMatches += score;
                }
            }

            if (totalMatches == 0)
                return [];

            return scores
                .Select(x => (Category: x.Key, Probability: Math.Round((double)x.Value / totalMatches, 4)))
                .OrderByDescending(x => x.Probability)
                .ToList();
        }

        /// <summary>
        /// Verifica se um produto pertence a uma categoria específica.
        /// </summary>
        public static bool BelongsToCategory(string? text, string category)
        {
            var inferred = InferCategory(text);
            return inferred != null && 
                   inferred.Equals(category, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Obtém todas as categorias disponíveis.
        /// </summary>
        public static List<string> GetAllCategories()
        {
            return CategoryKeywords.Keys.ToList();
        }

        /// <summary>
        /// Obtém palavras-chave de uma categoria.
        /// </summary>
        public static List<string> GetCategoryKeywords(string category)
        {
            return CategoryKeywords.TryGetValue(category, out var keywords) 
                ? keywords 
                : [];
        }
    }
}
