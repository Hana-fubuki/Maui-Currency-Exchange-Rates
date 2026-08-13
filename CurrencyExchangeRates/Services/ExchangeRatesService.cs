using System.Globalization;
using CurrencyExchangeRates.Models;

namespace CurrencyExchangeRates.Services;

public sealed class ExchangeRatesService(
	IFrankfurterApiClient apiClient,
	ICacheService cacheService) : IExchangeRatesService
{
	private static readonly DateOnly EarliestHistoricalDate = new(1999, 1, 4);
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
		if (string.Equals(baseCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
		{
			return CreateIdentityHistory(range);
		}

		var cacheKey = $"history:{baseCurrency}:{quoteCurrency}:{range}";
		if (cacheService.TryGetValue<IReadOnlyList<HistoricalRatePoint>>(cacheKey, out var cachedHistory) &&
			cachedHistory is not null)
		{
			return cachedHistory;
		}

		var fullHistory = await GetFullHistoricalRatesAsync(baseCurrency, quoteCurrency, cancellationToken);
		var normalizedHistory = ProjectHistoryForRange(fullHistory, range);
		cacheService.Set(cacheKey, normalizedHistory, HistoryCacheTtl);
		return normalizedHistory;
	}

	private async Task<IReadOnlyList<HistoricalRatePoint>> GetFullHistoricalRatesAsync(
		string baseCurrency,
		string quoteCurrency,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"history:{baseCurrency}:{quoteCurrency}:all";
		if (cacheService.TryGetValue<IReadOnlyList<HistoricalRatePoint>>(cacheKey, out var cachedHistory) &&
			cachedHistory is not null)
		{
			return cachedHistory;
		}

		var history = await apiClient.GetHistoricalRatesAsync(
			baseCurrency,
			quoteCurrency,
			EarliestHistoricalDate,
			groupBy: null,
			cancellationToken);

		cacheService.Set(cacheKey, history, HistoryCacheTtl);
		return history;
	}

	private static IReadOnlyList<HistoricalRatePoint> CreateIdentityHistory(TimeRangeKind range)
	{
		var request = HistoricalRequest.Create(range);
		return
		[
			new HistoricalRatePoint { Date = request.FromDate, Rate = 1m },
			new HistoricalRatePoint { Date = DateOnly.FromDateTime(DateTime.UtcNow), Rate = 1m }
		];
	}

	private static IReadOnlyList<HistoricalRatePoint> ProjectHistoryForRange(
		IReadOnlyList<HistoricalRatePoint> history,
		TimeRangeKind range)
	{
		if (history.Count == 0)
		{
			return Array.Empty<HistoricalRatePoint>();
		}

		var request = HistoricalRequest.Create(range);
		var rangeHistory = history
			.Where(point => point.Date >= request.FromDate)
			.ToArray();

		if (rangeHistory.Length == 0)
		{
			return Array.Empty<HistoricalRatePoint>();
		}

		rangeHistory = request.GroupBy switch
		{
			HistoricalGrouping.Week => GroupHistoryByWeek(rangeHistory),
			HistoricalGrouping.Month => GroupHistoryByMonth(rangeHistory),
			_ => rangeHistory
		};

		if (range == TimeRangeKind.FiveDays && rangeHistory.Length > 5)
		{
			return rangeHistory.TakeLast(5).ToArray();
		}

		if (range == TimeRangeKind.OneDay && rangeHistory.Length > 2)
		{
			return rangeHistory.TakeLast(2).ToArray();
		}

		return rangeHistory;
	}

	private static HistoricalRatePoint[] GroupHistoryByWeek(IEnumerable<HistoricalRatePoint> history)
	{
		return history
			.GroupBy(point => (Year: ISOWeek.GetYear(point.Date.ToDateTime(TimeOnly.MinValue)), Week: ISOWeek.GetWeekOfYear(point.Date.ToDateTime(TimeOnly.MinValue))))
			.Select(group => group.OrderBy(point => point.Date).Last())
			.OrderBy(point => point.Date)
			.ToArray();
	}

	private static HistoricalRatePoint[] GroupHistoryByMonth(IEnumerable<HistoricalRatePoint> history)
	{
		return history
			.GroupBy(point => (point.Date.Year, point.Date.Month))
			.Select(group => group.OrderBy(point => point.Date).Last())
			.OrderBy(point => point.Date)
			.ToArray();
	}

	private enum HistoricalGrouping
	{
		None,
		Week,
		Month
	}

	private sealed record HistoricalRequest(DateOnly FromDate, HistoricalGrouping GroupBy)
	{
		public static HistoricalRequest Create(TimeRangeKind range)
		{
			var today = DateOnly.FromDateTime(DateTime.UtcNow);

			return range switch
			{
				TimeRangeKind.OneDay => new HistoricalRequest(today.AddDays(-1), HistoricalGrouping.None),
				TimeRangeKind.FiveDays => new HistoricalRequest(today.AddDays(-5), HistoricalGrouping.None),
				TimeRangeKind.OneMonth => new HistoricalRequest(today.AddMonths(-1), HistoricalGrouping.None),
				TimeRangeKind.OneYear => new HistoricalRequest(today.AddYears(-1), HistoricalGrouping.Week),
				TimeRangeKind.FiveYears => new HistoricalRequest(today.AddYears(-5), HistoricalGrouping.Month),
				TimeRangeKind.Max => new HistoricalRequest(EarliestHistoricalDate, HistoricalGrouping.Month),
				_ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unsupported time range.")
			};
		}
	}
}
