using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.Interfaces
{
    public interface IPokemonPriceService
    {
        Task<decimal> GetCardMarketPriceAsync(string cardName, string cardNumber);
    }
}
