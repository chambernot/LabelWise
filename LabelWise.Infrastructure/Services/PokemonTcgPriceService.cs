using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LabelWise.Infrastructure.Services;

public class PokemonTcgPriceService : IPokemonPriceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PokemonTcgPriceService> _logger;

    public PokemonTcgPriceService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<PokemonTcgPriceService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["PokemonTcgApiKey"]
                     ?? configuration["PokemonTcg:ApiKey"]
                     ?? string.Empty;

        _httpClient.BaseAddress = new Uri("https://api.pokemontcg.io/v2/");

        // Cabeçalhos essenciais para contornar a rejeição SSL do Cloudflare
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
        }
    }

    public async Task<decimal> GetCardMarketPriceAsync(string cardName, string cardNumber)
    {
        try
        {
            var (cleanName, cleanNumber) = ParseCardDetails(cardName, cardNumber);

            if (string.IsNullOrWhiteSpace(cleanName))
                return 0m;

            // 1. Monta a busca Lucene limpa sem caracteres corrompidos
            string query = string.IsNullOrWhiteSpace(cleanNumber)
                ? $"name:\"{cleanName}\""
                : $"name:\"{cleanName}\" number:{cleanNumber}";

            var response = await _httpClient.GetAsync($"cards?q={query}");

            // 2. Fallback: Se retornar erro do servidor (500) ou não encontrar, tenta buscar apenas pelo nome
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[PokemonTcgPrice] ⚠️ Tentativa inicial falhou ({Status}). Executando fallback pelo nome...", response.StatusCode);
                response = await _httpClient.GetAsync($"cards?q=name:\"{cleanName}\"");

                if (!response.IsSuccessStatusCode) return 0m;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            {
                _logger.LogWarning("[PokemonTcgPrice] ⚠️ Nenhuma carta encontrada para: Name='{Name}'", cleanName);
                return 0m;
            }

            var card = data[0];

            // 3. Busca preço no TCGPlayer (USD)
            if (card.TryGetProperty("tcgplayer", out var tcgPlayer) &&
                tcgPlayer.TryGetProperty("prices", out var prices))
            {
                foreach (var priceType in prices.EnumerateObject())
                {
                    if (priceType.Value.TryGetProperty("market", out var marketElement) &&
                        marketElement.ValueKind == JsonValueKind.Number)
                    {
                        var price = marketElement.GetDecimal();
                        if (price > 0) return price;
                    }
                }
            }

            // 4. Fallback para Cardmarket
            if (card.TryGetProperty("cardmarket", out var cardmarket) &&
                cardmarket.TryGetProperty("prices", out var cmPrices))
            {
                if (cmPrices.TryGetProperty("trendPrice", out var trendElement) &&
                    trendElement.ValueKind == JsonValueKind.Number)
                {
                    return trendElement.GetDecimal();
                }
            }

            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PokemonTcgPrice] ❌ Erro ao consultar preço de mercado.");
            return 0m;
        }
    }

    private static (string Name, string Number) ParseCardDetails(string rawName, string rawNumber)
    {
        string name = rawName ?? string.Empty;
        string number = rawNumber ?? string.Empty;

        // Se o nome contiver o número (ex: "Pikachu (Secret Rare) - 115/114")
        if (name.Contains('-'))
        {
            var parts = name.Split('-');
            name = parts[0];
            if (string.IsNullOrWhiteSpace(number) && parts.Length > 1)
            {
                number = parts[1];
            }
        }

        // Remove sufixos como "(Secret Rare)" do nome
        if (name.Contains('('))
        {
            name = name.Split('(')[0];
        }

        // Se number for no formato "115/114", extrai apenas o numerador "115"
        if (number.Contains('/'))
        {
            number = number.Split('/')[0];
        }

        // Remove caracteres especiais, parênteses e espaços do número (mantém apenas letras e dígitos)
        number = new string(number.Where(char.IsLetterOrDigit).ToArray());

        // Se o número for inválido ou for um texto longo (ex: "SecretRare"), limpa para buscar apenas pelo nome
        if (number.Length > 6)
        {
            number = string.Empty;
        }

        return (name.Trim(), number.Trim());
    }
}