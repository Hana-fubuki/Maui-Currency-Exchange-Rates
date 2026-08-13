using CurrencyExchangeRates.Models;
using CurrencyExchangeRates.Services;

namespace CurrencyExchangeRates.Tests.Services;

public sealed class ExchangeRatesServiceTests
{
	[Fact]
	public async Task GetCurrenciesAsync_UsesCache_AfterFirstCall()
	{
		var api = new FakeFrankfurterApiClient
		{
			Currencies =
			[
				new CurrencyOption { Code = "USD", Name = "United States Dollar" }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var first = await service.GetCurrenciesAsync();
		var second = await service.GetCurrenciesAsync();

		Assert.Same(first, second);
		Assert.Equal(1, api.GetCurrenciesCallCount);
	}

	[Fact]
	public async Task GetLatestRateAsync_ReturnsOneForSameCurrency_WithoutCallingApi()
	{
		var api = new FakeFrankfurterApiClient();
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var snapshot = await service.GetLatestRateAsync("USD", "usd");

		Assert.Equal("USD", snapshot.BaseCurrency);
		Assert.Equal("usd", snapshot.QuoteCurrency);
		Assert.Equal(1m, snapshot.Rate);
		Assert.Equal(0, api.GetLatestRateCallCount);
	}

	[Fact]
	public async Task GetLatestRateAsync_CachesNonIdentityPair()
	{
		var api = new FakeFrankfurterApiClient
		{
			LatestSnapshot = new ExchangeRateSnapshot
			{
				BaseCurrency = "USD",
				QuoteCurrency = "EUR",
				Date = new DateOnly(2026, 1, 15),
				Rate = 0.91m
			}
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var first = await service.GetLatestRateAsync("USD", "EUR");
		var second = await service.GetLatestRateAsync("USD", "EUR");

		Assert.Same(first, second);
		Assert.Equal(1, api.GetLatestRateCallCount);
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_ReturnsSyntheticIdentityHistory()
	{
		var api = new FakeFrankfurterApiClient();
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var history = await service.GetHistoricalRatesAsync("JPY", "jpy", TimeRangeKind.OneMonth);

		Assert.Equal(2, history.Count);
		Assert.All(history, point => Assert.Equal(1m, point.Rate));
		Assert.Equal(0, api.GetHistoricalRatesCallCount);
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_NormalizesFiveDayRange_AndCachesResult()
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = today.AddDays(-6), Rate = 1m },
				new HistoricalRatePoint { Date = today.AddDays(-5), Rate = 2m },
				new HistoricalRatePoint { Date = today.AddDays(-4), Rate = 3m },
				new HistoricalRatePoint { Date = today.AddDays(-3), Rate = 4m },
				new HistoricalRatePoint { Date = today.AddDays(-2), Rate = 5m },
				new HistoricalRatePoint { Date = today.AddDays(-1), Rate = 6m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var first = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveDays);
		var second = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveDays);

		Assert.Equal([2m, 3m, 4m, 5m, 6m], first.Select(point => point.Rate).ToArray());
		Assert.Same(first, second);
		Assert.Equal(1, api.GetHistoricalRatesCallCount);
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_NormalizesOneDayRange_ToLatestTwoPoints()
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = today.AddDays(-2), Rate = 1m },
				new HistoricalRatePoint { Date = today.AddDays(-1), Rate = 2m },
				new HistoricalRatePoint { Date = today, Rate = 3m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var history = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.OneDay);

		Assert.Equal([2m, 3m], history.Select(point => point.Rate).ToArray());
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_FetchesFullHistoryOnce_AndReusesItAcrossRanges()
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var recentEarlier = today.Day > 1
			? today.AddDays(-1)
			: today;
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = today.AddYears(-6), Rate = 0.80m },
				new HistoricalRatePoint { Date = today.AddYears(-4), Rate = 0.90m },
				new HistoricalRatePoint { Date = recentEarlier, Rate = 1.00m },
				new HistoricalRatePoint { Date = today, Rate = 1.10m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.OneMonth);
		var max = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.Max);
		var fiveYears = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveYears);

		Assert.Equal(1, api.GetHistoricalRatesCallCount);
		Assert.Equal(new DateOnly(1999, 1, 4), api.LastHistoryFromDate);
		Assert.Null(api.LastHistoryGroupBy);
		Assert.Contains(max, point => point.Date <= today.AddYears(-6));
		Assert.DoesNotContain(fiveYears, point => point.Date <= today.AddYears(-6));
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_ReturnsEmptyHistory_WhenApiReturnsNoData()
	{
		var api = new FakeFrankfurterApiClient();
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var history = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.OneMonth);

		Assert.Empty(history);
		Assert.Equal(1, api.GetHistoricalRatesCallCount);
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_GroupsOneYearHistoryByWeek()
	{
		var today = DateTime.UtcNow.Date;
		var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
		var currentWeekMonday = DateOnly.FromDateTime(today.AddDays(-daysSinceMonday));
		var previousWeekMonday = currentWeekMonday.AddDays(-7);
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = previousWeekMonday.AddDays(1), Rate = 1.00m },
				new HistoricalRatePoint { Date = previousWeekMonday.AddDays(3), Rate = 1.10m },
				new HistoricalRatePoint { Date = currentWeekMonday.AddDays(1), Rate = 1.20m },
				new HistoricalRatePoint { Date = currentWeekMonday.AddDays(3), Rate = 1.30m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var history = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.OneYear);

		Assert.Equal([1.10m, 1.30m], history.Select(point => point.Rate).ToArray());
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_GroupsMaxHistoryByMonth_AndKeepsOlderDataThanFiveYears()
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var olderDate = today.AddYears(-6);
		var currentMonthEarlier = new DateOnly(today.Year, today.Month, 1);
		var currentMonthLater = currentMonthEarlier.AddDays(5);
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = olderDate, Rate = 0.70m },
				new HistoricalRatePoint { Date = currentMonthEarlier, Rate = 1.00m },
				new HistoricalRatePoint { Date = currentMonthLater, Rate = 1.05m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var maxHistory = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.Max);
		var fiveYearHistory = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveYears);

		Assert.Equal([0.70m, 1.05m], maxHistory.Select(point => point.Rate).ToArray());
		Assert.Equal([1.05m], fiveYearHistory.Select(point => point.Rate).ToArray());
	}

	private sealed class FakeFrankfurterApiClient : IFrankfurterApiClient
	{
		public IReadOnlyList<CurrencyOption> Currencies { get; set; } = [];
		public ExchangeRateSnapshot LatestSnapshot { get; set; } = new()
		{
			BaseCurrency = "USD",
			QuoteCurrency = "EUR",
			Date = new DateOnly(2026, 1, 1),
			Rate = 1.1m
		};
		public IReadOnlyList<HistoricalRatePoint> History { get; set; } = [];
		public int GetCurrenciesCallCount { get; private set; }
		public int GetLatestRateCallCount { get; private set; }
		public int GetHistoricalRatesCallCount { get; private set; }
		public DateOnly LastHistoryFromDate { get; private set; }
		public string? LastHistoryGroupBy { get; private set; }

		public Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
		{
			GetCurrenciesCallCount++;
			return Task.FromResult(Currencies);
		}

		public Task<ExchangeRateSnapshot> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
		{
			GetLatestRateCallCount++;
			return Task.FromResult(LatestSnapshot);
		}

		public Task<IReadOnlyList<HistoricalRatePoint>> GetHistoricalRatesAsync(
			string baseCurrency,
			string quoteCurrency,
			DateOnly fromDate,
			string? groupBy,
			CancellationToken cancellationToken = default)
		{
			GetHistoricalRatesCallCount++;
			LastHistoryFromDate = fromDate;
			LastHistoryGroupBy = groupBy;
			return Task.FromResult(History);
		}
	}
}
