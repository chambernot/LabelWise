using System;
using System.Threading.Tasks;
using LabelWise.Application.SummaryGeneration;

namespace LabelWise.Infrastructure.AI
{
    /// <summary>
    /// Exemplo de implementação futura do IAiProviderService usando Azure OpenAI.
    /// Este arquivo serve como template/guia para implementação real.
    /// 
    /// DEPENDÊNCIAS NECESSÁRIAS:
    /// - Azure.AI.OpenAI (NuGet package)
    /// 
    /// CONFIGURAÇÃO NECESSÁRIA (appsettings.json):
    /// {
    ///   "SummaryGeneration": {
    ///     "AiProvider": {
    ///       "Endpoint": "https://your-resource.openai.azure.com/",
    ///       "ApiKey": "your-api-key",
    ///       "ModelName": "gpt-4"
    ///     }
    ///   }
    /// }
    /// </summary>
    public class AzureOpenAiProvider : IAiProviderService
    {
        /* DESCOMENTAR QUANDO IMPLEMENTAR:
        
        private readonly OpenAIClient _client;
        private readonly string _deploymentName;
        private readonly AiProviderOptions _options;

        public AzureOpenAiProvider(IOptions<SummaryGenerationOptions> options)
        {
            _options = options.Value.AiProvider 
                ?? throw new ArgumentNullException(nameof(options), "AiProvider configuration is required");

            if (string.IsNullOrEmpty(_options.Endpoint) || string.IsNullOrEmpty(_options.ApiKey))
            {
                throw new InvalidOperationException("Azure OpenAI Endpoint and ApiKey must be configured");
            }

            _deploymentName = _options.ModelName ?? "gpt-4";

            // Cria cliente Azure OpenAI
            _client = new OpenAIClient(
                new Uri(_options.Endpoint),
                new AzureKeyCredential(_options.ApiKey)
            );
        }

        public async Task<string> GenerateCompletionAsync(string prompt)
        {
            try
            {
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(
                            "Você é um especialista em nutrição e análise de alimentos. " +
                            "Seu objetivo é gerar resumos claros, concisos e informativos sobre produtos alimentícios."
                        ),
                        new ChatRequestUserMessage(prompt)
                    },
                    MaxTokens = _options.MaxTokens,
                    Temperature = (float)_options.Temperature,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                };

                Response<ChatCompletions> response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
                
                if (response.Value.Choices.Count == 0)
                {
                    throw new InvalidOperationException("Azure OpenAI returned no completions");
                }

                return response.Value.Choices[0].Message.Content;
            }
            catch (RequestFailedException ex)
            {
                // Log erro e re-throw para ser tratado pelo fallback
                Console.WriteLine($"Azure OpenAI Error: {ex.Status} - {ex.Message}");
                throw new AiProviderException("Failed to generate AI summary", ex);
            }
        }
        
        */

        // IMPLEMENTAÇÃO TEMPORÁRIA (REMOVER QUANDO IMPLEMENTAR O CÓDIGO ACIMA)
        public Task<string> GenerateCompletionAsync(string prompt)
        {
            throw new NotImplementedException(
                "AzureOpenAiProvider não implementado ainda. " +
                "Descomente o código acima e adicione o pacote Azure.AI.OpenAI para implementar."
            );
        }
    }

    /// <summary>
    /// Exception customizada para erros de provedores de IA
    /// </summary>
    public class AiProviderException : Exception
    {
        public AiProviderException(string message) : base(message) { }
        public AiProviderException(string message, Exception innerException) : base(message, innerException) { }
    }
}

/*
 * PASSOS PARA IMPLEMENTAÇÃO COMPLETA:
 * 
 * 1. Adicionar pacote NuGet:
 *    dotnet add package Azure.AI.OpenAI --version 1.0.0-beta.17
 * 
 * 2. Descomentar o código da implementação acima
 * 
 * 3. Adicionar using statements:
 *    using Azure;
 *    using Azure.AI.OpenAI;
 *    using Microsoft.Extensions.Options;
 *    using LabelWise.Application.Configuration;
 * 
 * 4. Registrar no DI (Infrastructure/Extensions/ServiceCollectionExtensions.cs):
 *    services.AddScoped<IAiProviderService, AzureOpenAiProvider>();
 * 
 * 5. Configurar appsettings.json com suas credenciais Azure OpenAI
 * 
 * 6. Trocar Strategy para "AiPowered" na configuração
 * 
 * 7. Testar!
 * 
 * MONITORAMENTO E OTIMIZAÇÃO:
 * 
 * - Adicionar Application Insights para rastrear latência e custos
 * - Implementar cache de resumos para produtos já analisados
 * - Considerar rate limiting para controlar custos
 * - Adicionar circuit breaker para resiliência
 * - Implementar telemetria customizada para análise A/B
 * 
 * CUSTOS APROXIMADOS (Azure OpenAI GPT-4):
 * - Input: ~$0.03 por 1K tokens
 * - Output: ~$0.06 por 1K tokens
 * - Resumo típico: ~500 tokens input + 100 tokens output = ~$0.021 por análise
 * - 1000 análises/dia = ~$21/dia = ~$630/mês
 * 
 * ALTERNATIVAS DE CUSTO:
 * - GPT-3.5-Turbo: ~90% mais barato, qualidade levemente inferior
 * - Cache: Reduz chamadas em ~70% para produtos populares
 * - Batch processing: Pode obter descontos
 */
