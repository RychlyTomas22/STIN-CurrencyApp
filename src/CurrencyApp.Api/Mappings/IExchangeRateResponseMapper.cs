using CurrencyApp.Api.Models.ExchangeRateHost;
using CurrencyApp.Core.Models;

namespace CurrencyApp.Api.Mappings
{
    public interface IExchangeRateResponseMapper
    {
        ExchangeRateSnapshot MapLive(LiveRatesResponse response);
        ExchangeRateSnapshot MapHistorical(HistoricalRatesResponse response);
    }
}
