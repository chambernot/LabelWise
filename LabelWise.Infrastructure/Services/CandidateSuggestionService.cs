using System.Diagnostics;
using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.Helpers.ProductIdentification;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação MVP do serviço de sugestão de candidatos.
    /// 
    /// ESTRATÉGIAS IMPLEMENTADAS:
    /// 1. Busca por texto similar (fuzzy matching)
    /// 2. Busca por ingredientes similares
    /// 3. Busca por categoria inferida
    /// 4. Combinação e ranking de candidatos
    /// 
    /// PREPARAÇÃO PARA FUTURO:
    /// - Interface pronta para similaridade visual
    /// - Estrutura para integração com base externa
    /// - Hooks para Machine Learning ranking
    /// </summary>
    public class CandidateSuggestionService : ICandidateSuggestionService
    {
        private readonly ILogger<CandidateSuggestionService> _logger;
        private readonly IValidatedProductRepository _validatedProductRepository;

        // Threshold mínimo para considerar um candidato válido
        private const double MinimumCandidateConfidence = 0.30;

        // Base de produtos conhecidos (MVP: em memória, futuro: banco de dados)
        private static readonly List<KnownProduct> KnownProducts =
        [
            // Bebidas
            new() { Name = "Coca-Cola Original", Brand = "Coca-Cola", Category = "Bebida", Keywords = ["refrigerante", "cola", "cafeína"] },
            new() { Name = "Coca-Cola Zero", Brand = "Coca-Cola", Category = "Bebida", Keywords = ["refrigerante", "cola", "zero açúcar", "sem açúcar"] },
            new() { Name = "Guaraná Antarctica", Brand = "Antarctica", Category = "Bebida", Keywords = ["refrigerante", "guaraná", "brasileiro"] },
            new() { Name = "Fanta Laranja", Brand = "Coca-Cola", Category = "Bebida", Keywords = ["refrigerante", "laranja", "fanta"] },
            new() { Name = "Sprite", Brand = "Coca-Cola", Category = "Bebida", Keywords = ["refrigerante", "limão", "sem cor"] },
            new() { Name = "Red Bull Energy Drink", Brand = "Red Bull", Category = "Bebida", Keywords = ["energético", "cafeína", "taurina"] },
            new() { Name = "Suco Del Valle Laranja", Brand = "Del Valle", Category = "Bebida", Keywords = ["suco", "laranja", "néctar"] },
            
            // Laticínios
            new() { Name = "Leite Integral Parmalat", Brand = "Parmalat", Category = "Laticínio", Keywords = ["leite", "integral", "uht"] },
            new() { Name = "Iogurte Grego Danone", Brand = "Danone", Category = "Laticínio", Keywords = ["iogurte", "grego", "proteína"] },
            new() { Name = "Queijo Mussarela Sadia", Brand = "Sadia", Category = "Laticínio", Keywords = ["queijo", "mussarela", "fatiado"] },
            new() { Name = "Requeijão Cremoso Catupiry", Brand = "Catupiry", Category = "Laticínio", Keywords = ["requeijão", "cremoso", "original"] },
            
            // Snacks
            new() { Name = "Doritos Nacho", Brand = "Elma Chips", Category = "Snack", Keywords = ["salgadinho", "milho", "nacho", "queijo"] },
            new() { Name = "Ruffles Original", Brand = "Elma Chips", Category = "Snack", Keywords = ["batata", "chips", "ondulada"] },
            new() { Name = "Cheetos", Brand = "Elma Chips", Category = "Snack", Keywords = ["salgadinho", "milho", "queijo"] },
            new() { Name = "Biscoito Oreo", Brand = "Nabisco", Category = "Snack", Keywords = ["biscoito", "chocolate", "recheado", "creme"] },
            new() { Name = "Barra de Cereal Nutry", Brand = "Nutry", Category = "Snack", Keywords = ["barra", "cereal", "granola"] },
            
            // Cereais
            new() { Name = "Sucrilhos Kellogg's", Brand = "Kellogg's", Category = "Cereal", Keywords = ["cereal", "milho", "açucarado", "tigre"] },
            new() { Name = "Nescau Cereal", Brand = "Nestlé", Category = "Cereal", Keywords = ["cereal", "chocolate", "nescau"] },
            new() { Name = "Granola Tradicional", Brand = "Mãe Terra", Category = "Cereal", Keywords = ["granola", "aveia", "mel"] },
            
            // Doces
            new() { Name = "Chocolate Lacta ao Leite", Brand = "Lacta", Category = "Doce e Confeitaria", Keywords = ["chocolate", "ao leite", "barra"] },
            new() { Name = "Bis Lacta", Brand = "Lacta", Category = "Doce e Confeitaria", Keywords = ["chocolate", "wafer", "bis"] },
            new() { Name = "Nutella", Brand = "Ferrero", Category = "Doce e Confeitaria", Keywords = ["creme", "avelã", "chocolate", "pasta"] },
            
            // Pães e Padaria
            new() { Name = "Pão de Forma Seven Boys", Brand = "Seven Boys", Category = "Pão e Padaria", Keywords = ["pão", "forma", "integral"] },
            new() { Name = "Bisnaguinha Pullman", Brand = "Pullman", Category = "Pão e Padaria", Keywords = ["pão", "bisnaguinha", "macio"] },
            
            // Molhos
            new() { Name = "Ketchup Heinz", Brand = "Heinz", Category = "Molho e Condimento", Keywords = ["ketchup", "tomate", "condimento"] },
            new() { Name = "Maionese Hellmann's", Brand = "Hellmann's", Category = "Molho e Condimento", Keywords = ["maionese", "molho", "cremoso"] },
            new() { Name = "Molho de Tomate Pomarola", Brand = "Pomarola", Category = "Molho e Condimento", Keywords = ["molho", "tomate", "tradicional"] },
            
            // Congelados
            new() { Name = "Pizza Sadia Mussarela", Brand = "Sadia", Category = "Congelado", Keywords = ["pizza", "congelada", "mussarela"] },
            new() { Name = "Nuggets Sadia", Brand = "Sadia", Category = "Congelado", Keywords = ["nuggets", "frango", "empanado"] },
            new() { Name = "Lasanha Perdigão", Brand = "Perdigão", Category = "Congelado", Keywords = ["lasanha", "congelada", "bolonhesa"] }
        ];

        public CandidateSuggestionService(
            ILogger<CandidateSuggestionService> logger,
            IValidatedProductRepository validatedProductRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validatedProductRepository = validatedProductRepository ?? throw new ArgumentNullException(nameof(validatedProductRepository));
        }

        public async Task<CandidateSuggestionResult> SuggestCandidatesAsync(CandidateSuggestionRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("🔍 Iniciando sugestão de candidatos");
            _logger.LogInformation("   ExtractedText: {Text}", TruncateText(request.ExtractedText, 50));
            _logger.LogInformation("   Ingredientes: {Count}", request.PartialIngredients.Count);
            _logger.LogInformation("   Alergênicos: {Count}", request.Allergens.Count);
            _logger.LogInformation("   Categoria Inferida: {Category}", request.InferredCategory ?? "N/A");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var allCandidates = new List<SuggestedCandidate>();
            var strategiesUsed = new List<string>();

            try
            {
                // Estratégia 1: Busca por texto similar
                if (!string.IsNullOrWhiteSpace(request.ExtractedText))
                {
                    var textCandidates = await SearchByTextAsync(request.ExtractedText, request.MaxCandidates);
                    if (textCandidates.Count > 0)
                    {
                        allCandidates.AddRange(textCandidates);
                        strategiesUsed.Add("TextSimilarity");
                        _logger.LogInformation("✅ Texto: {Count} candidatos encontrados", textCandidates.Count);
                    }
                }

                // Estratégia 2: Busca por ingredientes
                if (request.PartialIngredients.Count > 0)
                {
                    var ingredientCandidates = await SearchByIngredientsAsync(
                        request.PartialIngredients, 
                        request.MaxCandidates);
                    if (ingredientCandidates.Count > 0)
                    {
                        allCandidates.AddRange(ingredientCandidates);
                        strategiesUsed.Add("IngredientMatch");
                        _logger.LogInformation("✅ Ingredientes: {Count} candidatos encontrados", ingredientCandidates.Count);
                    }
                }

                // Estratégia 3: Busca por categoria
                var category = request.InferredCategory ?? 
                               CategoryInferenceHelper.InferCategory(request.ExtractedText) ??
                               CategoryInferenceHelper.InferCategoryFromIngredients(request.PartialIngredients);

                if (!string.IsNullOrWhiteSpace(category))
                {
                    var categoryCandidates = await SearchByCategoryAsync(category, request.MaxCandidates);
                    if (categoryCandidates.Count > 0)
                    {
                        allCandidates.AddRange(categoryCandidates);
                        strategiesUsed.Add("CategoryMatch");
                        _logger.LogInformation("✅ Categoria ({Category}): {Count} candidatos encontrados", 
                            category, categoryCandidates.Count);
                    }
                }

                // Estratégia 4: Produtos validados do usuário (histórico)
                if (request.UserId.HasValue)
                {
                    var userCandidates = await SearchByUserHistoryAsync(request.UserId.Value, request);
                    if (userCandidates.Count > 0)
                    {
                        allCandidates.AddRange(userCandidates);
                        strategiesUsed.Add("UserHistory");
                        _logger.LogInformation("✅ Histórico: {Count} candidatos encontrados", userCandidates.Count);
                    }
                }

                // Estratégia 5: Similaridade visual (preparação arquitetural)
                if (request.VisualFeatures != null && request.VisualFeatures.Length > 0)
                {
                    var visualCandidates = await SearchByVisualSimilarityAsync(
                        request.VisualFeatures, 
                        request.MaxCandidates);
                    if (visualCandidates.Count > 0)
                    {
                        allCandidates.AddRange(visualCandidates);
                        strategiesUsed.Add("VisualSimilarity");
                    }
                }

                // Combinar e rankear candidatos
                var finalCandidates = CombineAndRankCandidates(allCandidates, request.MaxCandidates);

                // Filtrar por confiança mínima
                finalCandidates = finalCandidates
                    .Where(c => c.CandidateConfidence >= request.MinConfidence)
                    .ToList();

                stopwatch.Stop();

                var result = CandidateSuggestionResult.CreateWithCandidates(
                    finalCandidates,
                    "Identificação primária com baixa confiança",
                    strategiesUsed);

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                result.UsedVisualSimilarity = strategiesUsed.Contains("VisualSimilarity");
                result.Metadata["TotalStrategiesUsed"] = strategiesUsed.Count.ToString();
                result.Metadata["TotalCandidatesBeforeFilter"] = allCandidates.Count.ToString();
                result.Metadata["FinalCandidatesCount"] = finalCandidates.Count.ToString();

                _logger.LogInformation("═══════════════════════════════════════════════════════════");
                _logger.LogInformation("✅ Sugestão concluída em {Time:F3}s", result.ProcessingTimeSeconds);
                _logger.LogInformation("   Candidatos finais: {Count}", finalCandidates.Count);
                _logger.LogInformation("   Estratégias usadas: {Strategies}", string.Join(", ", strategiesUsed));
                _logger.LogInformation("═══════════════════════════════════════════════════════════");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro durante sugestão de candidatos");

                return new CandidateSuggestionResult
                {
                    IsProductUnknown = true,
                    FallbackReason = $"Erro durante sugestão: {ex.Message}",
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    UserMessage = "Ocorreu um erro ao buscar produtos similares. Por favor, insira manualmente."
                };
            }
        }

        public Task<List<SuggestedCandidate>> SearchByTextAsync(string text, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(new List<SuggestedCandidate>());

            var candidates = new List<SuggestedCandidate>();

            foreach (var product in KnownProducts)
            {
                // Similaridade com nome
                var nameSimilarity = TextSimilarityCalculator.CalculateCombinedSimilarity(text, product.Name);
                
                // Similaridade com marca
                var brandSimilarity = product.Brand != null 
                    ? TextSimilarityCalculator.CalculateCombinedSimilarity(text, product.Brand) 
                    : 0.0;

                // Similaridade com keywords
                var keywordSimilarity = product.Keywords
                    .Select(k => TextSimilarityCalculator.CalculateCombinedSimilarity(text, k))
                    .DefaultIfEmpty(0.0)
                    .Max();

                // Score combinado
                var combinedScore = Math.Max(nameSimilarity, Math.Max(brandSimilarity * 0.8, keywordSimilarity * 0.6));

                if (combinedScore >= MinimumCandidateConfidence)
                {
                    candidates.Add(new SuggestedCandidate
                    {
                        CandidateName = product.Name,
                        CandidateBrand = product.Brand,
                        Category = product.Category,
                        CandidateConfidence = Math.Round(combinedScore, 4),
                        MatchStrategy = CandidateMatchStrategy.TextSimilarity,
                        TextSimilarityScore = Math.Round(nameSimilarity, 4),
                        MatchReason = $"Similaridade textual: {nameSimilarity:P0}",
                        MatchDetails = new Dictionary<string, string>
                        {
                            ["NameSimilarity"] = nameSimilarity.ToString("F4"),
                            ["BrandSimilarity"] = brandSimilarity.ToString("F4"),
                            ["KeywordSimilarity"] = keywordSimilarity.ToString("F4")
                        }
                    });
                }
            }

            var result = candidates
                .OrderByDescending(c => c.CandidateConfidence)
                .Take(maxResults)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<List<SuggestedCandidate>> SearchByIngredientsAsync(
            List<string> ingredients, 
            int maxResults = 5)
        {
            if (ingredients == null || ingredients.Count == 0)
                return Task.FromResult(new List<SuggestedCandidate>());

            var candidates = new List<SuggestedCandidate>();

            foreach (var product in KnownProducts)
            {
                var ingredientSimilarity = TextSimilarityCalculator.CalculateListSimilarity(
                    ingredients, 
                    product.Keywords);

                if (ingredientSimilarity >= MinimumCandidateConfidence)
                {
                    candidates.Add(new SuggestedCandidate
                    {
                        CandidateName = product.Name,
                        CandidateBrand = product.Brand,
                        Category = product.Category,
                        CandidateConfidence = Math.Round(ingredientSimilarity * 0.85, 4), // Peso menor que texto
                        MatchStrategy = CandidateMatchStrategy.IngredientMatch,
                        IngredientSimilarityScore = Math.Round(ingredientSimilarity, 4),
                        MatchReason = $"Ingredientes similares: {ingredientSimilarity:P0}",
                        MatchDetails = new Dictionary<string, string>
                        {
                            ["MatchedKeywords"] = string.Join(", ", product.Keywords.Take(3))
                        }
                    });
                }
            }

            var result = candidates
                .OrderByDescending(c => c.CandidateConfidence)
                .Take(maxResults)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<List<SuggestedCandidate>> SearchByCategoryAsync(string category, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Task.FromResult(new List<SuggestedCandidate>());

            var normalizedCategory = TextSimilarityCalculator.NormalizeText(category);

            var candidates = KnownProducts
                .Where(p => TextSimilarityCalculator.NormalizeText(p.Category ?? "")
                    .Contains(normalizedCategory) ||
                    normalizedCategory.Contains(TextSimilarityCalculator.NormalizeText(p.Category ?? "")))
                .Select(p => new SuggestedCandidate
                {
                    CandidateName = p.Name,
                    CandidateBrand = p.Brand,
                    Category = p.Category,
                    CandidateConfidence = 0.40, // Confiança base para match por categoria
                    MatchStrategy = CandidateMatchStrategy.CategoryMatch,
                    MatchReason = $"Categoria: {category}",
                    MatchDetails = new Dictionary<string, string>
                    {
                        ["MatchedCategory"] = p.Category ?? ""
                    }
                })
                .Take(maxResults)
                .ToList();

            return Task.FromResult(candidates);
        }

        public Task<List<SuggestedCandidate>> SearchByVisualSimilarityAsync(
            double[] visualFeatures, 
            int maxResults = 5)
        {
            // PREPARAÇÃO ARQUITETURAL para futura implementação
            // TODO: Implementar quando houver:
            // 1. Extrator de features visuais (ex: embeddings de CNN)
            // 2. Base de dados de features de produtos conhecidos
            // 3. Algoritmo de busca por similaridade (ex: cosine similarity, FAISS)

            _logger.LogInformation("🔮 Similaridade visual: funcionalidade preparada para futuro");
            _logger.LogInformation("   Features recebidas: {Length} dimensões", visualFeatures?.Length ?? 0);

            // Retorna lista vazia por enquanto
            return Task.FromResult(new List<SuggestedCandidate>());
        }

        public List<SuggestedCandidate> CombineAndRankCandidates(
            IEnumerable<SuggestedCandidate> candidates, 
            int maxResults = 5)
        {
            if (candidates == null || !candidates.Any())
                return [];

            // Agrupar por nome do produto (deduplicar)
            var grouped = candidates
                .GroupBy(c => TextSimilarityCalculator.NormalizeText(c.CandidateName))
                .Select(g =>
                {
                    var best = g.OrderByDescending(c => c.CandidateConfidence).First();
                    var allStrategies = g.Select(c => c.MatchStrategy).Distinct().ToList();

                    // Boost de confiança se múltiplas estratégias encontraram o mesmo produto
                    if (allStrategies.Count > 1)
                    {
                        best.CandidateConfidence = Math.Min(1.0, best.CandidateConfidence * 1.15);
                        best.MatchStrategy = CandidateMatchStrategy.Combined;
                        best.MatchReason = $"Múltiplas estratégias: {string.Join(", ", allStrategies)}";
                        best.MatchDetails["CombinedStrategies"] = allStrategies.Count.ToString();
                    }

                    return best;
                })
                .OrderByDescending(c => c.CandidateConfidence)
                .Take(maxResults)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Busca candidatos baseado no histórico de produtos validados do usuário.
        /// </summary>
        private async Task<List<SuggestedCandidate>> SearchByUserHistoryAsync(
            int userId,
            CandidateSuggestionRequest request)
        {
            try
            {
                // Buscar produtos validados do usuário
                var validatedProducts = await _validatedProductRepository.GetByUserIdAsync(userId);

                if (validatedProducts == null || !validatedProducts.Any())
                    return [];

                var candidates = new List<SuggestedCandidate>();

                foreach (var product in validatedProducts.Take(20)) // Limitar busca
                {
                    var score = 0.0;

                    // Similaridade com texto extraído
                    if (!string.IsNullOrWhiteSpace(request.ExtractedText) && 
                        !string.IsNullOrWhiteSpace(product.ValidatedName))
                    {
                        var textSim = TextSimilarityCalculator.CalculateCombinedSimilarity(
                            request.ExtractedText, 
                            product.ValidatedName);
                        score = Math.Max(score, textSim);
                    }

                    // Similaridade com categoria
                    if (!string.IsNullOrWhiteSpace(request.InferredCategory) &&
                        !string.IsNullOrWhiteSpace(product.ValidatedCategory))
                    {
                        var catSim = TextSimilarityCalculator.CalculateSimilarity(
                            request.InferredCategory,
                            product.ValidatedCategory);
                        score = Math.Max(score, catSim * 0.7);
                    }

                    if (score >= MinimumCandidateConfidence)
                    {
                        candidates.Add(new SuggestedCandidate
                        {
                            ProductId = product.Id,
                            CandidateName = product.ValidatedName ?? "Produto Validado",
                            CandidateBrand = product.ValidatedBrand,
                            Category = product.ValidatedCategory,
                            CandidateConfidence = Math.Round(score * 0.9, 4), // Pequeno desconto
                            MatchStrategy = CandidateMatchStrategy.UserHistory,
                            Barcode = product.ValidatedBarcode,
                            MatchReason = "Produto previamente validado por você",
                            MatchDetails = new Dictionary<string, string>
                            {
                                ["ValidatedAt"] = product.CreatedAt.ToString("yyyy-MM-dd"),
                                ["ValidationSource"] = "UserHistory"
                            }
                        });
                    }
                }

                return candidates
                    .OrderByDescending(c => c.CandidateConfidence)
                    .Take(5)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao buscar histórico do usuário {UserId}", userId);
                return [];
            }
        }

        private static string TruncateText(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "N/A";

            return text.Length <= maxLength 
                ? text 
                : text[..maxLength] + "...";
        }

        /// <summary>
        /// Produto conhecido para MVP (futuro: migrar para banco de dados).
        /// </summary>
        private class KnownProduct
        {
            public required string Name { get; init; }
            public string? Brand { get; init; }
            public string? Category { get; init; }
            public List<string> Keywords { get; init; } = [];
            public string? Barcode { get; init; }
        }
    }
}
