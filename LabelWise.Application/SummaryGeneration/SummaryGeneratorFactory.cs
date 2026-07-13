using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Estratégias disponíveis para geração de resumo
    /// </summary>
    public enum SummaryGenerationStrategy
    {
        /// <summary>
        /// Geração baseada em regras simples (legado)
        /// </summary>
        RuleBased,

        /// <summary>
        /// Geração com IA (Azure OpenAI)
        /// </summary>
        AiPowered,

        /// <summary>
        /// Geração com consciência de confiança e completude (recomendado)
        /// </summary>
        ConfidenceAware
    }

    /// <summary>
    /// Factory para criar instâncias de IAnalysisSummaryGenerator baseado na configuração.
    /// Implementa o padrão Strategy permitindo alternância entre diferentes geradores.
    /// </summary>
    public class SummaryGeneratorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public SummaryGeneratorFactory(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        /// <summary>
        /// Cria o gerador apropriado baseado na configuração.
        /// </summary>
        public IAnalysisSummaryGenerator CreateGenerator()
        {
            var strategyConfig = _configuration.GetValue<string>("SummaryGeneration:Strategy");
            var strategy = ParseStrategy(strategyConfig);

            return CreateGenerator(strategy);
        }

        /// <summary>
        /// Cria um gerador específico baseado na estratégia fornecida.
        /// </summary>
        public IAnalysisSummaryGenerator CreateGenerator(SummaryGenerationStrategy strategy)
        {
            return strategy switch
            {
                SummaryGenerationStrategy.RuleBased => 
                    _serviceProvider.GetRequiredService<RuleBasedSummaryGenerator>(),

                SummaryGenerationStrategy.AiPowered => 
                    _serviceProvider.GetRequiredService<AiSummaryGenerator>(),

                SummaryGenerationStrategy.ConfidenceAware =>
                    _serviceProvider.GetRequiredService<ConfidenceAwareSummaryGenerator>(),

                _ => throw new ArgumentException($"Estratégia de geração não suportada: {strategy}")
            };
        }

        /// <summary>
        /// Obtém a estratégia configurada atualmente.
        /// </summary>
        public SummaryGenerationStrategy GetConfiguredStrategy()
        {
            var strategyConfig = _configuration.GetValue<string>("SummaryGeneration:Strategy");
            return ParseStrategy(strategyConfig);
        }

        private SummaryGenerationStrategy ParseStrategy(string? strategyConfig)
        {
            if (string.IsNullOrEmpty(strategyConfig))
            {
                // Default: ConfidenceAware (mais seguro)
                return SummaryGenerationStrategy.ConfidenceAware;
            }

            if (Enum.TryParse<SummaryGenerationStrategy>(strategyConfig, ignoreCase: true, out var strategy))
            {
                return strategy;
            }

            // Fallback para ConfidenceAware se configuração for inválida
            return SummaryGenerationStrategy.ConfidenceAware;
        }
    }
}
