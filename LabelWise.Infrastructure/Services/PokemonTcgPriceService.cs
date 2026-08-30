using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

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

            // 1. Monta a expressão Lucene pura
            string rawQuery = string.IsNullOrWhiteSpace(cleanNumber)
                ? $"name:\"{cleanName}\""
                : $"name:\"{cleanName}\" number:{cleanNumber}";

            // 2. CODIFICAÇÃO COMPLETA: Garante %22 para aspas e %20 para espaços
            string encodedQuery = Uri.EscapeDataString(rawQuery);
            string requestUrl = $"https://api.pokemontcg.io/v2/cards?q={encodedQuery}";

            var response = await _httpClient.GetAsync(requestUrl);

            // 3. Fallback: Se a busca com número falhar, tenta apenas pelo nome codificado
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[PokemonTcgPrice] ⚠️ Busca inicial falhou ({Status}). Tentando fallback por nome...", response.StatusCode);

                string fallbackQuery = Uri.EscapeDataString($"name:\"{cleanName}\"");
                string fallbackUrl = $"https://api.pokemontcg.io/v2/cards?q={fallbackQuery}";

                response = await _httpClient.GetAsync(fallbackUrl);

                if (!response.IsSuccessStatusCode) return 0m;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            {
                _logger.LogWarning("[PokemonTcgPrice] ⚠️ Nenhuma carta encontrada para: Name='{Name}' Number='{Number}'", cleanName, cleanNumber);
                return 0m;
            }

            var card = data[0];

            // 4. Busca preço no TCGPlayer (USD)
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

            // 5. Fallback para Cardmarket
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

        // Extrai o número do nome se vier junto (ex: "Pikachu 115/114" ou "Pikachu 115")
        if (string.IsNullOrWhiteSpace(number))
        {
            var match = Regex.Match(name, @"^(.*?)\s+([A-Za-z0-9]+(?:/[A-Za-z0-9]+)?)$");
            if (match.Success)
            {
                name = match.Groups[1].Value;
                number = match.Groups[2].Value;
            }
        }

        if (name.Contains('-'))
        {
            var parts = name.Split('-');
            name = parts[0];
            if (string.IsNullOrWhiteSpace(number) && parts.Length > 1)
            {
                number = parts[1];
            }
        }

        if (name.Contains('('))
        {
            name = name.Split('(')[0];
        }

        if (number.Contains('/'))
        {
            number = number.Split('/')[0];
        }

        number = new string(number.Where(char.IsLetterOrDigit).ToArray());

        if (number.Length > 6)
        {
            number = string.Empty;
        }

        return (name.Trim(), number.Trim());
    }
}