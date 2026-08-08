using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces;

public interface ICardMarketPricingService
{
    Task<decimal> GetEstimatedPriceAsync(string cardName, CardCondition condition);
}