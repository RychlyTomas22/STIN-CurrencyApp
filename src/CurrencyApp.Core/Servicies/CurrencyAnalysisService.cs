using CurrencyApp.Core.Models;

namespace CurrencyApp.Core.Services
{
    public class CurrencyAnalysisService : ICurrencyAnalysisService
    {
        public CurrencyAnalysisResult Analyze(
            ExchangeRateSnapshot currentSnapshot,
            IEnumerable<ExchangeRateSnapshot> historicalSnapshots)
        {
            if (currentSnapshot is null)
            {
                throw new ArgumentNullException(nameof(currentSnapshot));
            }

            if (currentSnapshot.Rates is null || currentSnapshot.Rates.Count == 0)
            {
                throw new ArgumentException("Current snapshot must contain at least one rate.", nameof(currentSnapshot));
            }

            var historicalList = historicalSnapshots?.ToList() ?? new List<ExchangeRateSnapshot>();

            ValidateBaseCurrencies(currentSnapshot, historicalList);

            var strongestCurrency = currentSnapshot.Rates
                .OrderByDescending(x => x.Rate)
                .ThenBy(x => x.Currency)
                .First();

            var weakestCurrency = currentSnapshot.Rates
                .OrderBy(x => x.Rate)
                .ThenBy(x => x.Currency)
                .First();

            var averageRates = historicalList
                .SelectMany(snapshot => snapshot.Rates)
                .GroupBy(rate => rate.Currency)
                .Select(group => new CurrencyAverageResult
                {
                    Currency = group.Key,
                    AverageRate = decimal.Round(group.Average(x => x.Rate), 6),
                    SampleCount = group.Count()
                })
                .OrderBy(x => x.Currency)
                .ToList();

            return new CurrencyAnalysisResult
            {
                BaseCurrency = currentSnapshot.BaseCurrency,
                CurrentDate = currentSnapshot.Date,
                StrongestCurrency = new ExchangeRateValue
                {
                    Currency = strongestCurrency.Currency,
                    Rate = strongestCurrency.Rate
                },
                WeakestCurrency = new ExchangeRateValue
                {
                    Currency = weakestCurrency.Currency,
                    Rate = weakestCurrency.Rate
                },
                AverageRates = averageRates
            };
        }

        private static void ValidateBaseCurrencies(
            ExchangeRateSnapshot currentSnapshot,
            IEnumerable<ExchangeRateSnapshot> historicalSnapshots)
        {
            foreach (var snapshot in historicalSnapshots)
            {
                if (!string.Equals(
                        currentSnapshot.BaseCurrency,
                        snapshot.BaseCurrency,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "All snapshots must use the same base currency.");
                }
            }
        }
    }
}