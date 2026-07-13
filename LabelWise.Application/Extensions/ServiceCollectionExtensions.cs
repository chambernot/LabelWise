using Microsoft.Extensions.DependencyInjection;
using LabelWise.Application.Rules;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Services;
using LabelWise.Application.SummaryGeneration;
using LabelWise.Application.Configuration;
using LabelWise.Application.Parsing;

namespace LabelWise.Application
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register application layer services, handlers, validators, mappers
            // services.AddScoped<IProductService, ProductService>();

            // Authentication services
            services.AddScoped<Interfaces.IAuthService, Services.AuthService>();
            services.AddScoped<Interfaces.IJwtTokenService, Services.JwtTokenService>();
            services.AddSingleton<Interfaces.IPasswordHasher, Services.PasswordHasher>();
            services.AddScoped<Interfaces.IUserProfileService, Services.UserProfileService>();

            // Product analysis engine and rules
            services.AddScoped<IProductAnalysisEngine, ProductAnalysisEngineService>();

            // Register rules (extensible collection)
            // ORDEM IMPORTA: Regras são executadas na ordem de registro
            services.AddScoped<IRule, NutrientScoringRule>();
            services.AddScoped<IRule, UltraProcessedProductRule>(); // Nova regra para produtos ultraprocessados
            services.AddScoped<IRule, AllergenAndIngredientRules>();
            services.AddScoped<IRule, RecommendationsRule>();

            // Ingredient and Allergen Parser
            services.AddScoped<IIngredientAllergenParser, IngredientAllergenParser>();

            // ═══════════════════════════════════════════════════════════════════
            // Summary Generation Strategy Pattern
            // ═══════════════════════════════════════════════════════════════════

            // Registra todas as implementações de gerador de resumo
            services.AddScoped<RuleBasedSummaryGenerator>();
            services.AddScoped<AiSummaryGenerator>();
            services.AddScoped<ConfidenceAwareSummaryGenerator>(); // NOVO: Gerador consciente de confiança

            // Factory para escolher a estratégia baseada em configuração
            services.AddScoped<SummaryGeneratorFactory>();

            // Resolve o gerador ativo baseado na configuração
            // Default é ConfidenceAware para garantir mensagens seguras
            services.AddScoped<IAnalysisSummaryGenerator>(sp =>
            {
                var factory = sp.GetRequiredService<SummaryGeneratorFactory>();
                return factory.CreateGenerator();
            });

            // Product analysis registration moved to Infrastructure layer (implementation depends on DbContext/storage)

            return services;
        }
    }
}
