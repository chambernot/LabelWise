# ═══════════════════════════════════════════════════════════════════════
# KNOWN PRODUCTS CATALOG - PostgreSQL Full-Text Search
# ═══════════════════════════════════════════════════════════════════════
# 
# Implementação de busca de produtos usando PostgreSQL como alternativa
# econômica ao Azure AI Search.
#
# OBJETIVO:
# - Catálogo local de produtos conhecidos
# - Busca textual aproximada (full-text search)
# - Ranking por relevância
# - Fallback para identificação de produtos
#
# PREPARAÇÃO PARA MIGRAÇÃO:
# - Interface abstrata permite trocar por Azure AI Search ou pgvector
# - DTOs independentes de tecnologia
# - Scores normalizados (0.0 a 1.0)
#
# ═══════════════════════════════════════════════════════════════════════

using LabelWise.Application.DTOs.KnownProducts;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso do catálogo de produtos conhecidos
    /// </summary>
    public class KnownProductsCatalogExamples
    {
        private readonly IKnownProductRepository _repository;
        private readonly IKnownProductSearchService _searchService;

        public KnownProductsCatalogExamples(
            IKnownProductRepository repository,
            IKnownProductSearchService searchService)
        {
            _repository = repository;
            _searchService = searchService;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: Adicionar produtos ao catálogo
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example1_AddKnownProducts()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 1: Adicionar produtos conhecidos ao catálogo");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Produto 1: Coca-Cola
            var cocaCola = new KnownProduct
            {
                Name = "Coca-Cola Original",
                Brand = "Coca-Cola",
                Category = "Refrigerante",
                Barcode = "7894900011517",
                Keywords = "coca cola refrigerante lata pet garrafa",
                KnownFrontText = "Coca-Cola Original Refrigerante",
                IsValidated = true
            };

            await _repository.AddAsync(cocaCola);
            Console.WriteLine($"✅ Produto adicionado: {cocaCola.Name} - {cocaCola.Brand}");
            Console.WriteLine($"   ID: {cocaCola.Id}");
            Console.WriteLine($"   Barcode: {cocaCola.Barcode}\n");

            // Produto 2: Nescau
            var nescau = new KnownProduct
            {
                Name = "Nescau Achocolatado em Pó",
                Brand = "Nestlé",
                Category = "Achocolatado",
                Barcode = "7891000100103",
                Keywords = "nescau nestle achocolatado chocolate pó",
                KnownFrontText = "Nescau Achocolatado em Pó Nestlé",
                KnownIngredients = "Açúcar, Cacau em pó, Vitaminas, Minerais",
                IsValidated = true
            };

            await _repository.AddAsync(nescau);
            Console.WriteLine($"✅ Produto adicionado: {nescau.Name} - {nescau.Brand}");
            Console.WriteLine($"   ID: {nescau.Id}");
            Console.WriteLine($"   Barcode: {nescau.Barcode}\n");

            // Produto 3: Toddy
            var toddy = new KnownProduct
            {
                Name = "Toddy Original",
                Brand = "Pepsico",
                Category = "Achocolatado",
                Barcode = "7896007800469",
                Keywords = "toddy achocolatado chocolate pó pepsico",
                KnownFrontText = "Toddy Original Achocolatado em Pó",
                IsValidated = true
            };

            await _repository.AddAsync(toddy);
            Console.WriteLine($"✅ Produto adicionado: {toddy.Name} - {toddy.Brand}\n");

            // Produto 4: Bis Chocolate
            var bis = new KnownProduct
            {
                Name = "Bis Xtra Chocolate",
                Brand = "Lacta",
                Category = "Biscoito",
                Barcode = "7622300990060",
                Keywords = "bis biscoito chocolate lacta wafer",
                KnownFrontText = "Bis Xtra Chocolate Lacta",
                IsValidated = true
            };

            await _repository.AddAsync(bis);
            Console.WriteLine($"✅ Produto adicionado: {bis.Name} - {bis.Brand}\n");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: Busca por código de barras
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example2_SearchByBarcode()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 2: Busca por código de barras");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var barcode = "7894900011517"; // Coca-Cola

            Console.WriteLine($"🔍 Buscando produto com barcode: {barcode}");

            var result = await _searchService.SearchByBarcodeAsync(barcode);

            if (result != null)
            {
                Console.WriteLine("\n✅ PRODUTO ENCONTRADO:");
                Console.WriteLine($"   Nome: {result.Name}");
                Console.WriteLine($"   Marca: {result.Brand}");
                Console.WriteLine($"   Categoria: {result.Category}");
                Console.WriteLine($"   Barcode: {result.Barcode}");
                Console.WriteLine($"   Score: {result.RelevanceScore:P2}");
                Console.WriteLine($"   Match Source: {result.MatchSource}");
                Console.WriteLine($"   Validado: {result.IsValidated}");
                Console.WriteLine($"   Identificações: {result.IdentificationCount}x\n");
            }
            else
            {
                Console.WriteLine("❌ Produto não encontrado\n");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: Busca textual - Nome do produto
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example3_SearchByProductName()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 3: Busca textual por nome do produto");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var searchQuery = "coca cola";

            Console.WriteLine($"🔍 Buscando: '{searchQuery}'");

            var request = new KnownProductSearchRequest
            {
                SearchQuery = searchQuery,
                MaxResults = 5,
                MinConfidence = 0.0
            };

            var response = await _searchService.SearchAsync(request);

            if (response.Success)
            {
                Console.WriteLine($"\n✅ {response.Results.Count} resultado(s) encontrado(s)");
                Console.WriteLine($"⏱️  Tempo de busca: {response.SearchTimeSeconds:F3}s\n");

                foreach (var result in response.Results)
                {
                    Console.WriteLine($"📦 {result.Name} - {result.Brand}");
                    Console.WriteLine($"   Score: {result.RelevanceScore:P2}");
                    Console.WriteLine($"   Razão: {result.MatchReason}");
                    Console.WriteLine($"   Categoria: {result.Category}");
                    Console.WriteLine($"   Barcode: {result.Barcode ?? "N/A"}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine($"❌ Erro: {response.ErrorMessage}\n");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: Busca textual - Marca
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example4_SearchByBrand()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 4: Busca textual por marca");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var searchQuery = "nestle";

            Console.WriteLine($"🔍 Buscando marca: '{searchQuery}'");

            var request = new KnownProductSearchRequest
            {
                SearchQuery = searchQuery,
                MaxResults = 10
            };

            var response = await _searchService.SearchAsync(request);

            if (response.Success && response.Results.Count > 0)
            {
                Console.WriteLine($"\n✅ {response.Results.Count} produto(s) da marca encontrado(s)\n");

                foreach (var result in response.Results)
                {
                    Console.WriteLine($"📦 {result.Name}");
                    Console.WriteLine($"   Marca: {result.Brand}");
                    Console.WriteLine($"   Score: {result.RelevanceScore:P2}\n");
                }
            }
            else
            {
                Console.WriteLine("❌ Nenhum produto encontrado\n");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: Busca fuzzy (tolerância a erros)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example5_FuzzySearch()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 5: Busca fuzzy (tolerância a erros de digitação)");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var queries = new[]
            {
                "coca kola",      // erro de digitação
                "neskau",         // erro de digitação
                "achocolatado",   // categoria
                "bisc chocolate"  // palavras parciais
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"🔍 Buscando: '{query}'");

                var request = new KnownProductSearchRequest
                {
                    SearchQuery = query,
                    MaxResults = 3,
                    EnableFuzzySearch = true,
                    MinConfidence = 0.3 // threshold mais baixo para fuzzy
                };

                var response = await _searchService.SearchAsync(request);

                if (response.Success && response.Results.Count > 0)
                {
                    Console.WriteLine($"   ✅ {response.Results.Count} resultado(s):");
                    foreach (var result in response.Results.Take(2))
                    {
                        Console.WriteLine($"      • {result.Name} - {result.Brand} ({result.RelevanceScore:P0})");
                    }
                }
                else
                {
                    Console.WriteLine("   ❌ Nenhum resultado");
                }
                Console.WriteLine();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: Busca por categoria
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example6_SearchByCategory()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 6: Busca filtrada por categoria");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var category = "Achocolatado";

            Console.WriteLine($"🔍 Buscando produtos da categoria: {category}");

            var products = await _repository.GetByCategoryAsync(category);

            Console.WriteLine($"\n✅ {products.Count} produto(s) encontrado(s):\n");

            foreach (var product in products)
            {
                Console.WriteLine($"📦 {product.Name} - {product.Brand}");
                Console.WriteLine($"   Identificações: {product.IdentificationCount}x");
                Console.WriteLine($"   Validado: {product.IsValidated}\n");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 7: Produtos mais populares
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example7_MostPopularProducts()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 7: Produtos mais identificados (ranking)");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var popularProducts = await _repository.GetMostPopularAsync(10);

            Console.WriteLine($"🏆 TOP {popularProducts.Count} PRODUTOS MAIS IDENTIFICADOS:\n");

            int rank = 1;
            foreach (var product in popularProducts)
            {
                Console.WriteLine($"{rank}. {product.Name} - {product.Brand}");
                Console.WriteLine($"   Identificações: {product.IdentificationCount}x");
                Console.WriteLine($"   Última identificação: {product.LastIdentifiedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"}\n");
                rank++;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 8: Sugestões de auto-complete
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example8_AutocompleteSuggestions()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 8: Sugestões de auto-complete");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var partialQueries = new[] { "coc", "nes", "bisc" };

            foreach (var partial in partialQueries)
            {
                Console.WriteLine($"💡 Digite '{partial}' → sugestões:");

                var response = await _searchService.SuggestAsync(partial, maxResults: 3);

                if (response.Success && response.Results.Count > 0)
                {
                    foreach (var suggestion in response.Results)
                    {
                        Console.WriteLine($"   • {suggestion.Name} ({suggestion.Brand})");
                    }
                }
                else
                {
                    Console.WriteLine("   (nenhuma sugestão)");
                }
                Console.WriteLine();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 9: Integração com ProductIdentificationService
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example9_IntegrationWithProductIdentification()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 9: Integração com ProductIdentificationService");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            Console.WriteLine("FLUXO DE IDENTIFICAÇÃO:");
            Console.WriteLine("1. Barcode (se fornecido) → busca em KnownProducts");
            Console.WriteLine("2. OCR frontal → busca em KnownProducts");
            Console.WriteLine("3. OCR + Vision → busca em KnownProducts");
            Console.WriteLine("4. Sugestão de candidatos (fallback final)\n");

            Console.WriteLine("EXEMPLO DE BUSCA FALLBACK:");
            Console.WriteLine("- OCR extraiu: 'Coca Cola Original'");
            Console.WriteLine("- Buscando em produtos conhecidos...");

            var request = new KnownProductSearchRequest
            {
                SearchQuery = "Coca Cola Original",
                MaxResults = 3,
                MinConfidence = 0.60 // threshold para identificação automática
            };

            var response = await _searchService.SearchAsync(request);

            if (response.Success && response.Results.Count > 0)
            {
                var best = response.Results.First();

                if (best.RelevanceScore >= 0.60)
                {
                    Console.WriteLine($"✅ PRODUTO IDENTIFICADO:");
                    Console.WriteLine($"   Nome: {best.Name}");
                    Console.WriteLine($"   Marca: {best.Brand}");
                    Console.WriteLine($"   Confiança: {best.RelevanceScore:P2}");
                    Console.WriteLine($"   Match confiável: {(best.RelevanceScore >= 0.70 ? "SIM" : "NÃO")}\n");

                    // Registrar identificação
                    var product = await _repository.GetByIdAsync(best.ProductId);
                    if (product != null)
                    {
                        product.RecordIdentification();
                        await _repository.UpdateAsync(product);
                        Console.WriteLine($"   📊 Identificação registrada (total: {product.IdentificationCount}x)\n");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Match encontrado mas confiança baixa: {best.RelevanceScore:P2}");
                    Console.WriteLine("   → Sugerindo candidatos ao usuário\n");
                }
            }
            else
            {
                Console.WriteLine("❌ Nenhum produto conhecido encontrado");
                Console.WriteLine("   → Partindo para sugestão de candidatos\n");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 10: Estatísticas do catálogo
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example10_CatalogStatistics()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 10: Estatísticas do catálogo");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var totalCount = await _repository.GetTotalCountAsync();
            var validatedCount = (await _repository.GetValidatedProductsAsync()).Count;
            var popularProducts = await _repository.GetMostPopularAsync(5);

            Console.WriteLine($"📊 ESTATÍSTICAS DO CATÁLOGO:");
            Console.WriteLine($"   Total de produtos: {totalCount}");
            Console.WriteLine($"   Produtos validados: {validatedCount}");
            Console.WriteLine($"   Taxa de validação: {(validatedCount * 100.0 / Math.Max(totalCount, 1)):F1}%\n");

            Console.WriteLine($"🔥 TOP 5 MAIS POPULARES:");
            foreach (var product in popularProducts)
            {
                Console.WriteLine($"   • {product.Name} ({product.IdentificationCount}x)");
            }
            Console.WriteLine();
        }
    }
}
