using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Application.SummaryGeneration;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso do novo sistema de geração de resumos.
    /// Demonstra diferentes cenários e estratégias.
    /// </summary>
    public class SummaryGenerationExamples
    {
        // EXEMPLO 1: Uso básico com DI (cenário mais comum)
        public class ProductAnalysisController
        {
            private readonly IProductAnalysisEngine _analysisEngine;

            public ProductAnalysisController(IProductAnalysisEngine analysisEngine)
            {
                // O engine já vem configurado com o gerador apropriado
                _analysisEngine = analysisEngine;
            }

            public ProductAnalysisResultDto AnalyzeProduct(Product product, NutritionalInfo nutrition)
            {
                var ingredients = new List<ProductIngredient>();
                var allergens = new List<ProductAllergen>();
                var userProfile = new UserProfile();

                // O resumo será gerado automaticamente usando a estratégia configurada
                var result = _analysisEngine.Analyze(product, nutrition, ingredients, allergens, userProfile);
                
                // result.Summary já contém o resumo gerado pela estratégia ativa
                return result;
            }
        }

        // EXEMPLO 2: Comparação de estratégias (para análise ou A/B testing)
        public class SummaryComparisonService
        {
            private readonly SummaryGeneratorFactory _factory;

            public SummaryComparisonService(SummaryGeneratorFactory factory)
            {
                _factory = factory;
            }

            public ComparisonResult CompareSummaryStrategies(
                Product product,
                NutritionalInfo nutrition,
                double generalScore,
                double personalizedScore)
            {
                var ingredients = new List<ProductIngredient>();
                var allergens = new List<ProductAllergen>();
                var alerts = new List<string> { "Alto teor de sódio", "Contém glúten" };
                var recommendations = new List<string> { "Consumir com moderação" };

                // Gera com estratégia baseada em regras
                var ruleBasedGenerator = _factory.CreateGenerator(SummaryGenerationStrategy.RuleBased);
                var ruleBasedSummary = ruleBasedGenerator.GenerateSummary(
                    product, nutrition, ingredients, allergens, null,
                    generalScore, personalizedScore, alerts, recommendations
                );

                // Gera com estratégia de IA (quando implementada)
                var aiGenerator = _factory.CreateGenerator(SummaryGenerationStrategy.AiPowered);
                var aiSummary = aiGenerator.GenerateSummary(
                    product, nutrition, ingredients, allergens, null,
                    generalScore, personalizedScore, alerts, recommendations
                );

                return new ComparisonResult
                {
                    RuleBasedSummary = ruleBasedSummary,
                    AiSummary = aiSummary,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        public class ComparisonResult
        {
            public string RuleBasedSummary { get; set; } = string.Empty;
            public string AiSummary { get; set; } = string.Empty;
            public DateTime GeneratedAt { get; set; }
        }

        // EXEMPLO 3: Implementação com fallback manual (cenário avançado)
        public class ResilientSummaryService
        {
            private readonly IAnalysisSummaryGenerator _primaryGenerator;
            private readonly RuleBasedSummaryGenerator _fallbackGenerator;

            public ResilientSummaryService(
                IAnalysisSummaryGenerator primaryGenerator,
                RuleBasedSummaryGenerator fallbackGenerator)
            {
                _primaryGenerator = primaryGenerator;
                _fallbackGenerator = fallbackGenerator;
            }

            public string GenerateWithFallback(
                Product product,
                NutritionalInfo nutrition,
                double generalScore,
                double personalizedScore)
            {
                var ingredients = new List<ProductIngredient>();
                var allergens = new List<ProductAllergen>();
                var alerts = new List<string>();
                var recommendations = new List<string>();

                try
                {
                    // Tenta com estratégia principal
                    return _primaryGenerator.GenerateSummary(
                        product, nutrition, ingredients, allergens, null,
                        generalScore, personalizedScore, alerts, recommendations
                    );
                }
                catch (Exception ex)
                {
                    // Log erro
                    Console.WriteLine($"Primary generator failed: {ex.Message}");

                    // Fallback para estratégia baseada em regras
                    return _fallbackGenerator.GenerateSummary(
                        product, nutrition, ingredients, allergens, null,
                        generalScore, personalizedScore, alerts, recommendations
                    );
                }
            }
        }

        // EXEMPLO 4: Mudança dinâmica de estratégia (cenário de feature flags)
        public class DynamicStrategyService
        {
            private readonly SummaryGeneratorFactory _factory;

            public DynamicStrategyService(SummaryGeneratorFactory factory)
            {
                _factory = factory;
            }

            public string GenerateBasedOnUserTier(
                string userTier,
                Product product,
                NutritionalInfo nutrition,
                double generalScore,
                double personalizedScore)
            {
                var ingredients = new List<ProductIngredient>();
                var allergens = new List<ProductAllergen>();
                var alerts = new List<string>();
                var recommendations = new List<string>();

                // Usuários premium recebem resumos de IA
                // Usuários free recebem resumos baseados em regras
                var strategy = userTier == "Premium" 
                    ? SummaryGenerationStrategy.AiPowered 
                    : SummaryGenerationStrategy.RuleBased;

                var generator = _factory.CreateGenerator(strategy);

                return generator.GenerateSummary(
                    product, nutrition, ingredients, allergens, null,
                    generalScore, personalizedScore, alerts, recommendations
                );
            }
        }

        // EXEMPLO 5: Geração em lote com estratégias diferentes
        public class BatchSummaryService
        {
            private readonly RuleBasedSummaryGenerator _ruleGenerator;
            private readonly AiSummaryGenerator _aiGenerator;

            public BatchSummaryService(
                RuleBasedSummaryGenerator ruleGenerator,
                AiSummaryGenerator aiGenerator)
            {
                _ruleGenerator = ruleGenerator;
                _aiGenerator = aiGenerator;
            }

            public async Task<List<BatchSummaryResult>> GenerateBatchSummariesAsync(
                List<ProductAnalysisData> products)
            {
                var results = new List<BatchSummaryResult>();

                foreach (var productData in products)
                {
                    // Gera com ambas estratégias para comparação
                    var ruleBasedSummary = _ruleGenerator.GenerateSummary(
                        productData.Product,
                        productData.Nutrition,
                        productData.Ingredients,
                        productData.Allergens,
                        null,
                        productData.GeneralScore,
                        productData.PersonalizedScore,
                        productData.Alerts,
                        productData.Recommendations
                    );

                    var aiSummary = _aiGenerator.GenerateSummary(
                        productData.Product,
                        productData.Nutrition,
                        productData.Ingredients,
                        productData.Allergens,
                        null,
                        productData.GeneralScore,
                        productData.PersonalizedScore,
                        productData.Alerts,
                        productData.Recommendations
                    );

                    results.Add(new BatchSummaryResult
                    {
                        ProductName = productData.Product.Name,
                        RuleBasedSummary = ruleBasedSummary,
                        AiSummary = aiSummary
                    });

                    // Evita rate limiting em chamadas de IA
                    await Task.Delay(100);
                }

                return results;
            }
        }

        public class ProductAnalysisData
        {
            public Product Product { get; set; } = new Product();
            public NutritionalInfo Nutrition { get; set; } = new NutritionalInfo();
            public List<ProductIngredient> Ingredients { get; set; } = new();
            public List<ProductAllergen> Allergens { get; set; } = new();
            public double GeneralScore { get; set; }
            public double PersonalizedScore { get; set; }
            public List<string> Alerts { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }

        public class BatchSummaryResult
        {
            public string ProductName { get; set; } = string.Empty;
            public string RuleBasedSummary { get; set; } = string.Empty;
            public string AiSummary { get; set; } = string.Empty;
        }
    }
}

/*
 * CENÁRIOS DE USO POR CASO:
 * 
 * 1. PRODUÇÃO PADRÃO (Exemplo 1):
 *    - Mais comum e simples
 *    - Usa configuração do appsettings.json
 *    - Troca de estratégia sem código
 * 
 * 2. A/B TESTING (Exemplo 2):
 *    - Compara estratégias para decisão
 *    - Útil para validar qualidade da IA
 *    - Pode usar métricas de satisfação do usuário
 * 
 * 3. ALTA DISPONIBILIDADE (Exemplo 3):
 *    - Fallback automático se IA falhar
 *    - Garante serviço sempre disponível
 *    - Crítico para aplicações production
 * 
 * 4. FEATURE FLAGS (Exemplo 4):
 *    - Estratégias diferentes por segmento
 *    - IA para usuários premium
 *    - Controle de custos por tier
 * 
 * 5. PROCESSAMENTO EM LOTE (Exemplo 5):
 *    - Análise bulk de produtos
 *    - Comparação para treinamento
 *    - Geração de datasets
 * 
 * CONFIGURAÇÃO TÍPICA (appsettings.json):
 * 
 * DESENVOLVIMENTO:
 * {
 *   "SummaryGeneration": {
 *     "Strategy": "RuleBased"  // Sem custos, rápido
 *   }
 * }
 * 
 * STAGING:
 * {
 *   "SummaryGeneration": {
 *     "Strategy": "AiPowered",
 *     "EnableFallback": true,   // Fallback para testes
 *     "AiProvider": {
 *       "Endpoint": "https://staging-openai.azure.com/",
 *       "ApiKey": "staging-key"
 *     }
 *   }
 * }
 * 
 * PRODUÇÃO:
 * {
 *   "SummaryGeneration": {
 *     "Strategy": "AiPowered",
 *     "EnableFallback": true,   // Garante disponibilidade
 *     "AiTimeoutSeconds": 5,    // Timeout curto
 *     "AiProvider": {
 *       "Provider": "AzureOpenAI",
 *       "Endpoint": "https://prod-openai.azure.com/",
 *       "ApiKey": "***",        // Use Azure Key Vault
 *       "ModelName": "gpt-4",
 *       "Temperature": 0.7,
 *       "MaxTokens": 150        // Controle de custo
 *     }
 *   }
 * }
 */
