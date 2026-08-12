using CurrencyExchangeRates.Models;

namespace CurrencyExchangeRates.Services;

public sealed class ExchangeRatesService(
	IFrankfurterApiClient apiClient,
	ICacheService cacheService) : IExchangeRatesService
{
	private static readonly TimeSpan CurrencyCacheTtl = TimeSpan.FromHours(24);
	private static readonly TimeSpan LatestRateCacheTtl = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan HistoryCacheTtl = TimeSpan.FromMinutes(15);

	public async Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
	{
		const string cacheKey = "currencies";

		if (cacheService.TryGetValue<IReadOnlyList<CurrencyOption>>(cacheKey, out var cachedCurrencies) &&
			cachedCurrencies is not null)
		{
			return cachedCurrencies;
		}

		var currencies = await apiClient.GetCurrenciesAsync(cancellationToken);
		cacheService.Set(cacheKey, currencies, CurrencyCacheTtl);
		return currencies;
	}

	public async Task<ExchangeRateSnapshot> GetLatestRateAsync(
		string baseCurrency,
		string quoteCurrency,
		CancellationToken cancellationToken = default)
	{
		if (string.Equals(baseCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
		{
			return new ExchangeRateSnapshot
			{
				BaseCurrency = baseCurrency,
				QuoteCurrency = quoteCurrency,
				Date = DateOnly.FromDateTime(DateTime.UtcNow),
				Rate = 1m
			};
		}

		var cacheKey = $"latest:{baseCurrency}:{quoteCurrency}";
		if (cacheService.TryGetValue<ExchangeRateSnapshot>(cacheKey, out var cachedRate) && cachedRate is not null)
		{
			return cachedRate;
		}

		var snapshot = await apiClient.GetLatestRateAsync(baseCurrency, quoteCurrency, cancellationToken);
		cacheService.Set(cacheKey, snapshot, LatestRateCacheTtl);
		return snapshot;
	}

	public async Task<IReadOnlyList<HistoricalRatePoint>> GetHistoricalRatesAsync(
		string baseCurrency,
		string quoteCurrency,
		TimeRangeKind range,
		CancellationToken cancellationToken = default)
	{
		var request = HistoricalRequest.Create(range);

		if (string.Equals(baseCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
		{
			return
			[
				new HistoricalRatePoint { Date = request.FromDate, Rate = 1m },
				new HistoricalRatePoint { Date = DateOnly.FromDateTime(DateTime.UtcNow), Rate = 1m }
			];
		}

		var cacheKey = $"history:{baseCurrency}:{quoteCurrency}:{range}";
		if (cacheService.TryGetValue<IReadOnlyList<HistoricalRatePoint>>(cacheKey, out var cachedHistory) &&
			cachedHistory is not null)
		{
			return cachedHistory;
		}

		var history = await apiClient.GetHistoricalRatesAsync(
			baseCurrency,
			quoteCurrency,
			request.FromDate,
			request.GroupBy,
			cancellationToken);

		var normalizedHistory = NormalizeHistory(history, range);
		cacheService.Set(cacheKey, normalizedHistory, HistoryCacheTtl);
		return normalizedHistory;
	}

	private static IReadOnlyList<HistoricalRatePoint> NormalizeHistory(
		IReadOnlyList<HistoricalRatePoint> history,
		TimeRangeKind range)
	{
		if (history.Count == 0)
		{
			return Array.Empty<HistoricalRatePoint>();
		}

		if (range == TimeRangeKind.FiveDays && history.Count > 5)
		{
			return history.TakeLast(5).ToArray();
		}

		if (range == TimeRangeKind.OneDay && history.Count > 2)
		{
			return history.TakeLast(2).ToArray();
		}

		return history;
	}

	private sealed record HistoricalRequest(DateOnly FromDate, string? GroupBy)
	{
		public static HistoricalRequest Create(TimeRangeKind range)
		{
			var today = DateOnly.FromDateTime(DateTime.UtcNow);

			return range switch
			{
				TimeRangeKind.OneDay => new HistoricalRequest(today.AddDays(-1), null),
				TimeRangeKind.FiveDays => new HistoricalRequest(today.AddDays(-5), null),
				TimeRangeKind.OneMonth => new HistoricalRequest(today.AddMonths(-1), null),
				TimeRangeKind.OneYear => new HistoricalRequest(today.AddYears(-1), "week"),
				TimeRangeKind.FiveYears => new HistoricalRequest(today.AddYears(-5), "month"),
				TimeRangeKind.Max => new HistoricalRequest(new DateOnly(1999, 1, 4), "month"),
				_ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unsupported time range.")
			};
		}
	}
}
