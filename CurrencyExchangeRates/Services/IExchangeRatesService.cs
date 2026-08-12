using CurrencyExchangeRates.Models;

namespace CurrencyExchangeRates.Services;

public interface IExchangeRatesService
{
	Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

	Task<ExchangeRateSnapshot> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<HistoricalRatePoint>> GetHistoricalRatesAsync(
		string baseCurrency,
		string quoteCurrency,
		TimeRangeKind range,
		CancellationToken cancellationToken = default);
}
