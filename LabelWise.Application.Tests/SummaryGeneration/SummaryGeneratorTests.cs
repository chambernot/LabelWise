using System;
using System.Collections.Generic;
using Xunit;
using LabelWise.Application.Interfaces;
using LabelWise.Application.SummaryGeneration;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.SummaryGeneration
{
    /// <summary>
    /// Testes unitários para RuleBasedSummaryGenerator
    /// 
    /// PARA EXECUTAR:
    /// 1. Criar projeto de testes:
    ///    dotnet new xunit -n LabelWise.Application.Tests
    /// 2. Adicionar referência:
    ///    dotnet add reference ../LabelWise.Application/LabelWise.Application.csproj
    ///    dotnet add reference ../LabelWise.Domain/LabelWise.Domain.csproj
    /// 3. Executar:
    ///    dotnet test
    /// </summary>
    public class RuleBasedSummaryGeneratorTests
    {
        private readonly RuleBasedSummaryGenerator _generator;

        public RuleBasedSummaryGeneratorTests()
        {
            _generator = new RuleBasedSummaryGenerator();
        }

        [Fact]
        public void StrategyName_ShouldReturn_RuleBased()
        {
            // Act
            var strategyName = _generator.StrategyName;

            // Assert
            Assert.Equal("RuleBased", strategyName);
        }

        [Fact]
        public void GenerateSummary_WithHighScores_ShouldReturn_ExcelenteEscolha()
        {
            // Arrange
            var product = CreateTestProduct("Produto Saudável");
            var nutrition = CreateHealthyNutrition();
            var generalScore = 0.85;
            var personalizedScore = 0.80;

            // Act
            var summary = _generator.GenerateSummary(
                product, nutrition, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, generalScore, personalizedScore, new List<string>(), new List<string>()
            );

            // Assert
            Assert.Contains("Excelente Escolha", summary);
            Assert.Contains("85%", summary);
        }

        [Fact]
        public void GenerateSummary_WithLowScores_ShouldReturn_Evitar()
        {
            // Arrange
            var product = CreateTestProduct("Produto Não Saudável");
            var nutrition = CreateUnhealthyNutrition();
            var generalScore = 0.25;
            var personalizedScore = 0.30;

            // Act
            var summary = _generator.GenerateSummary(
                product, nutrition, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, generalScore, personalizedScore, new List<string>(), new List<string>()
            );

            // Assert
            Assert.Contains("Evitar", summary);
        }

        [Fact]
        public void GenerateSummary_WithHighCalories_ShouldHighlight()
        {
            // Arrange
            var product = CreateTestProduct("Produto Calórico");
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(calories: 500); // Alto em calorias

            // Act
            var summary = _generator.GenerateSummary(
                product, nutrition, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, new List<string>(), new List<string>()
            );

            // Assert
            Assert.Contains("Alto em calorias", summary);
            Assert.Contains("500", summary);
        }

        [Fact]
        public void GenerateSummary_WithHighSugar_ShouldHighlight()
        {
            // Arrange
            var product = CreateTestProduct("Produto Açucarado");
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(sugars: 30); // Alto teor de açúcar

            // Act
            var summary = _generator.GenerateSummary(
                product, nutrition, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, new List<string>(), new List<string>()
            );

            // Assert
            Assert.Contains("Alto teor de açúcar", summary);
        }

        [Fact]
        public void GenerateSummary_WithAlerts_ShouldIncludeAlertCount()
        {
            // Arrange
            var product = CreateTestProduct("Produto com Alertas");
            var alerts = new List<string> { "Alerta 1", "Alerta 2", "Alerta 3" };

            // Act
            var summary = _generator.GenerateSummary(
                product, null, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, alerts, new List<string>()
            );

            // Assert
            Assert.Contains("3 alerta(s)", summary);
            Assert.Contains("⚠️", summary);
        }

        [Fact]
        public void GenerateSummary_WithRecommendations_ShouldIncludeCount()
        {
            // Arrange
            var product = CreateTestProduct("Produto com Recomendações");
            var recommendations = new List<string> { "Rec 1", "Rec 2" };

            // Act
            var summary = _generator.GenerateSummary(
                product, null, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, new List<string>(), recommendations
            );

            // Assert
            Assert.Contains("2 recomendação(ões)", summary);
            Assert.Contains("💡", summary);
        }

        [Fact]
        public void GenerateSummary_WithUserProfile_ShouldIncludeGoal()
        {
            // Arrange
            var product = CreateTestProduct("Produto");
            var userProfile = new UserProfile(
                Guid.NewGuid(),
                GoalType.WeightLoss,
                glutenFree: true
            );

            // Act
            var summary = _generator.GenerateSummary(
                product, null, new List<ProductIngredient>(), new List<ProductAllergen>(),
                userProfile, 0.5, 0.5, new List<string>(), new List<string>()
            );

            // Assert
            Assert.Contains("WeightLoss", summary);
        }

        [Fact]
        public void GenerateSummary_WithNullNutrition_ShouldNotThrow()
        {
            // Arrange
            var product = CreateTestProduct("Produto sem Info Nutricional");

            // Act
            var summary = _generator.GenerateSummary(
                product, null, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, new List<string>(), new List<string>()
            );

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
        }

        [Fact]
        public void GenerateSummary_WithNullUserProfile_ShouldNotThrow()
        {
            // Arrange
            var product = CreateTestProduct("Produto");
            var nutrition = CreateHealthyNutrition();

            // Act
            var summary = _generator.GenerateSummary(
                product, nutrition, new List<ProductIngredient>(), new List<ProductAllergen>(),
                null, 0.5, 0.5, new List<string>(), new List<string>()
            );

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
        }

        // Helper methods
        private Product CreateTestProduct(string name)
        {
            return new Product(name, "Test Brand", Guid.NewGuid());
        }

        private NutritionalInfo CreateHealthyNutrition()
        {
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(
                calories: 150,
                totalFat: 3,
                carbs: 20,
                sugars: 5,
                protein: 12,
                fiber: 8,
                sodium: 200
            );
            return nutrition;
        }

        private NutritionalInfo CreateUnhealthyNutrition()
        {
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(
                calories: 600,
                totalFat: 35,
                carbs: 80,
                sugars: 45,
                protein: 2,
                fiber: 1,
                sodium: 1500
            );
            return nutrition;
        }
    }

    /// <summary>
    /// Testes para SummaryGeneratorFactory
    /// </summary>
    public class SummaryGeneratorFactoryTests
    {
        [Fact]
        public void CreateGenerator_WithRuleBasedStrategy_ShouldReturnRuleBasedGenerator()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var configuration = CreateConfiguration("RuleBased");
            var factory = new SummaryGeneratorFactory(serviceProvider, configuration);

            // Act
            var generator = factory.CreateGenerator(SummaryGenerationStrategy.RuleBased);

            // Assert
            Assert.IsType<RuleBasedSummaryGenerator>(generator);
            Assert.Equal("RuleBased", generator.StrategyName);
        }

        [Fact]
        public void CreateGenerator_WithAiStrategy_ShouldReturnAiGenerator()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var configuration = CreateConfiguration("AiPowered");
            var factory = new SummaryGeneratorFactory(serviceProvider, configuration);

            // Act
            var generator = factory.CreateGenerator(SummaryGenerationStrategy.AiPowered);

            // Assert
            Assert.IsType<AiSummaryGenerator>(generator);
            Assert.Equal("AI-Powered", generator.StrategyName);
        }

        [Fact]
        public void GetConfiguredStrategy_WithRuleBasedConfig_ShouldReturnRuleBased()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var configuration = CreateConfiguration("RuleBased");
            var factory = new SummaryGeneratorFactory(serviceProvider, configuration);

            // Act
            var strategy = factory.GetConfiguredStrategy();

            // Assert
            Assert.Equal(SummaryGenerationStrategy.RuleBased, strategy);
        }

        [Fact]
        public void GetConfiguredStrategy_WithNoConfig_ShouldDefaultToRuleBased()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var configuration = CreateConfiguration(null);
            var factory = new SummaryGeneratorFactory(serviceProvider, configuration);

            // Act
            var strategy = factory.GetConfiguredStrategy();

            // Assert
            Assert.Equal(SummaryGenerationStrategy.RuleBased, strategy);
        }

        // Helper methods
        private IServiceProvider CreateServiceProvider()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddScoped<RuleBasedSummaryGenerator>();
            services.AddScoped<AiSummaryGenerator>();
            return services.BuildServiceProvider();
        }

        private Microsoft.Extensions.Configuration.IConfiguration CreateConfiguration(string? strategy)
        {
            var configData = new Dictionary<string, string?>();
            if (strategy != null)
            {
                configData["SummaryGeneration:Strategy"] = strategy;
            }

            return new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();
        }
    }
}

/*
 * COMANDOS PARA CRIAR E EXECUTAR TESTES:
 * 
 * # 1. Criar projeto de testes (se não existir)
 * dotnet new xunit -n LabelWise.Application.Tests -o LabelWise.Application.Tests
 * 
 * # 2. Adicionar ao solution
 * dotnet sln add LabelWise.Application.Tests/LabelWise.Application.Tests.csproj
 * 
 * # 3. Adicionar referências
 * cd LabelWise.Application.Tests
 * dotnet add reference ../LabelWise.Application/LabelWise.Application.csproj
 * dotnet add reference ../LabelWise.Domain/LabelWise.Domain.csproj
 * 
 * # 4. Executar testes
 * dotnet test
 * 
 * # 5. Executar com cobertura
 * dotnet test --collect:"XPlat Code Coverage"
 * 
 * # 6. Executar testes específicos
 * dotnet test --filter "FullyQualifiedName~RuleBasedSummaryGeneratorTests"
 * 
 * RESULTADO ESPERADO:
 * ✅ 11+ testes passando
 * ✅ Cobertura > 80%
 */
