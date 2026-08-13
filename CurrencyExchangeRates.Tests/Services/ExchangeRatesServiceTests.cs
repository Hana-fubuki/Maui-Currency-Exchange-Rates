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
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 1), Rate = 1m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 2), Rate = 2m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 3), Rate = 3m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 4), Rate = 4m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 5), Rate = 5m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 6), Rate = 6m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		var first = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveDays);
		var second = await service.GetHistoricalRatesAsync("USD", "EUR", TimeRangeKind.FiveDays);

		Assert.Equal([2m, 3m, 4m, 5m, 6m], first.Select(point => point.Rate).ToArray());
		Assert.Same(first, second);
		Assert.Equal(1, api.GetHistoricalRatesCallCount);
	}

	[Theory]
	[InlineData(TimeRangeKind.OneYear, "week")]
	[InlineData(TimeRangeKind.FiveYears, "month")]
	[InlineData(TimeRangeKind.Max, "month")]
	public async Task GetHistoricalRatesAsync_UsesExpectedGrouping(TimeRangeKind range, string expectedGroupBy)
	{
		var api = new FakeFrankfurterApiClient
		{
			History =
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 1), Rate = 1m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 2), Rate = 2m }
			]
		};
		var service = new ExchangeRatesService(api, new MemoryCacheService());

		await service.GetHistoricalRatesAsync("USD", "EUR", range);

		Assert.Equal(expectedGroupBy, api.LastHistoryGroupBy);
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
			LastHistoryGroupBy = groupBy;
			return Task.FromResult(History);
		}
	}
}
