using System.Net.Http.Json;
using LabelWise.Application.DTOs.OpenFoodFacts;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class OpenFoodFactsService : IOpenFoodFactsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenFoodFactsService> _logger;

        private const string BaseUrl = "https://world.openfoodfacts.org/api/v2/product/";

        public OpenFoodFactsService(HttpClient httpClient, ILogger<OpenFoodFactsService> logger)
        {
            _httpClient = httpClient;
            _logger     = logger;
        }

        public async Task<OpenFoodFactsProduct?> GetByBarcodeAsync(string barcode)
        {
            try
            {
                var url = $"{BaseUrl}{barcode}.json";
                var response = await _httpClient.GetFromJsonAsync<OpenFoodFactsResponse>(url);

                if (response?.Status == 1 && response.Product != null)
                {
                    if (!response.Product.HasUsableNutritionData())
                    {
                        _logger.LogInformation(
                            "OpenFoodFacts: produto encontrado para barcode {Barcode} mas sem dados nutricionais",
                            barcode);
                        return null;
                    }

                    _logger.LogInformation(
                        "OpenFoodFacts: produto encontrado para barcode {Barcode}",
                        barcode);
                    return response.Product;
                }

                _logger.LogInformation("OpenFoodFacts: produto não encontrado para barcode {Barcode}", barcode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenFoodFacts: erro ao buscar barcode {Barcode}", barcode);
                return null;
            }
        }
    }
}
