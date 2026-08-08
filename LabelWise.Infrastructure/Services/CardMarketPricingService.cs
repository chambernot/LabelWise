using System.Text.Json;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public class CardMarketPricingService : ICardMarketPricingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CardMarketPricingService> _logger;

    // Cotação simulada para o exemplo. Em produção, você pode chamar a API do Banco Central ou AwesomeAPI.
    private const decimal UsdToBrlRate = 5.20m;

    public CardMarketPricingService(IHttpClientFactory httpClientFactory, ILogger<CardMarketPricingService> logger)
    {
        // Usamos um client nomeado caso você queira configurar políticas de retry (Polly) depois
        _httpClient = httpClientFactory.CreateClient("PricingApi");
        _httpClient.BaseAddress = new Uri("https://api.scryfall.com/");
        _logger = logger;
    }

    public async Task<decimal> GetEstimatedPriceAsync(string cardName, CardCondition condition)
    {
        try
        {
            _logger.LogInformation("[Pricing] Buscando preço real para: {CardName}", cardName);

            // Chamada real para a API pública (exemplo focado em Magic: The Gathering)
            var response = await _httpClient.GetAsync($"cards/named?exact={Uri.EscapeDataString(cardName)}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Pricing] Carta não encontrada na API. Retornando valor base 0.");
                return 0m;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            // Tenta pegar o preço em USD (se não tiver, pega o foil)
            var priceString = json.RootElement.GetProperty("prices").GetProperty("usd").GetString()
                              ?? json.RootElement.GetProperty("prices").GetProperty("usd_foil").GetString();

            if (!decimal.TryParse(priceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal basePriceUsd))
            {
                return 0m;
            }

            // Converte para Reais (NM = Preço cheio)
            decimal basePriceBrl = basePriceUsd * UsdToBrlRate;

            // Aplica a depreciação padrão de mercado baseada na condição detectada pela IA
            decimal finalPrice = condition switch
            {
                CardCondition.NM => basePriceBrl,
                CardCondition.SP => basePriceBrl * 0.85m, // Perde 15%
                CardCondition.MP => basePriceBrl * 0.65m, // Perde 35%
                CardCondition.HP => basePriceBrl * 0.45m, // Perde 55%
                _ => basePriceBrl * 0.20m // Damaged
            };

            _logger.LogInformation("[Pricing] Preço calculado: R$ {Price} (Condição: {Condition})", finalPrice, condition);

            return Math.Round(finalPrice, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pricing] Erro ao buscar preço da carta.");
            return 0m;
        }
    }
}