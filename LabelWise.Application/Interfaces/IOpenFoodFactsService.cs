using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.DTOs.OpenFoodFacts;

namespace LabelWise.Application.Interfaces
{
    public interface IOpenFoodFactsService
    {
        Task<OpenFoodFactsProduct?> GetByBarcodeAsync(string barcode);
    }
}
