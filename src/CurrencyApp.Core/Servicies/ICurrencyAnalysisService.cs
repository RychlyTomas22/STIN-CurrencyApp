using CurrencyApp.Core.Models;

namespace CurrencyApp.Core.Services
{
    public interface ICurrencyAnalysisService
    {
        CurrencyAnalysisResult Analyze(
            ExchangeRateSnapshot currentSnapshot,
            IEnumerable<ExchangeRateSnapshot> historicalSnapshots);
    }
}