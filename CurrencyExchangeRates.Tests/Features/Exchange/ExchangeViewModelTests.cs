using CurrencyExchangeRates.Features.Exchange;
using CurrencyExchangeRates.Models;
using CurrencyExchangeRates.Services;
using Microsoft.Maui.Graphics;

namespace CurrencyExchangeRates.Tests.Features.Exchange;

public sealed class ExchangeViewModelTests
{
	[Fact]
	public async Task InitializeAsync_LoadsDefaults_CurrentRate_AndHistory()
	{
		var service = CreateService(
			latestRate: 0.92m,
			history:
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 0.88m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 11), Rate = 0.90m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 12), Rate = 0.92m }
			]);
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();

		Assert.Equal("USD", viewModel.SelectedBaseCurrency?.Code);
		Assert.Equal("EUR", viewModel.SelectedQuoteCurrency?.Code);
		Assert.Equal("1 United States Dollar equals", viewModel.DisplayBaseSummaryText);
		Assert.Equal("Euro", viewModel.DisplayQuoteNameText);
		Assert.Equal("0.9200", viewModel.DisplayRateText);
		Assert.Equal("1", viewModel.BaseAmountText);
		Assert.Equal("0.92", viewModel.QuoteAmountText);
		Assert.Equal("Updated Jan 15, 2026", viewModel.DisplayDateText);
		Assert.Equal("1M", viewModel.CurrentRangeText);
		Assert.False(viewModel.IsChartEmpty);
		Assert.Equal(3, viewModel.HistoricalPoints.Count);
		Assert.Equal(viewModel.HistoricalPoints[^1], viewModel.HighlightedPoint);
		Assert.Equal([TimeRangeKind.OneMonth], service.HistoryRequests);
	}

	[Fact]
	public async Task BaseAmountText_RecalculatesQuoteAmount()
	{
		var viewModel = new ExchangeViewModel(CreateService(latestRate: 2m));
		await viewModel.InitializeAsync();

		viewModel.BaseAmountText = "2.5";

		Assert.Equal("5", viewModel.QuoteAmountText);
	}

	[Fact]
	public async Task QuoteAmountText_RecalculatesBaseAmount()
	{
		var viewModel = new ExchangeViewModel(CreateService(latestRate: 4m));
		await viewModel.InitializeAsync();

		viewModel.QuoteAmountText = "10";

		Assert.Equal("2.5", viewModel.BaseAmountText);
	}

	[Fact]
	public async Task SelectTimeRangeCommand_UpdatesSelection_AndReloadsData()
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);
		await viewModel.InitializeAsync();

		var targetRange = viewModel.TimeRanges.Single(range => range.Kind == TimeRangeKind.OneYear);
		viewModel.SelectTimeRangeCommand.Execute(targetRange);
		await WaitForConditionAsync(() => service.HistoryRequests.Count >= 2);

		Assert.Equal(TimeRangeKind.OneYear, viewModel.SelectedTimeRange?.Kind);
		Assert.Equal([TimeRangeKind.OneMonth, TimeRangeKind.OneYear], service.HistoryRequests);
		Assert.True(targetRange.IsSelected);
		Assert.Single(viewModel.TimeRanges, range => range.IsSelected);
		Assert.Equal("1Y", viewModel.CurrentRangeText);
	}

	[Fact]
	public async Task ChangeSummary_AndChartColors_ReflectPositiveTrend()
	{
		var viewModel = new ExchangeViewModel(CreateService(
			history:
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 0.80m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 11), Rate = 0.90m }
			]));
		await viewModel.InitializeAsync();

		Assert.StartsWith("+0.1000 (+12.50%)", viewModel.ChangeSummaryText);
		AssertColorEquals(Color.FromArgb("#2E7D32"), viewModel.ChangeSummaryColor);
		AssertColorEquals(Color.FromArgb("#188038"), viewModel.ChartLineColor);
	}

	[Fact]
	public async Task ChangeSummary_AndChartColors_ReflectNegativeTrend()
	{
		var viewModel = new ExchangeViewModel(CreateService(
			history:
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 1.00m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 11), Rate = 0.75m }
			]));
		await viewModel.InitializeAsync();

		Assert.StartsWith("-0.2500 (-25.00%)", viewModel.ChangeSummaryText);
		AssertColorEquals(Color.FromArgb("#C62828"), viewModel.ChangeSummaryColor);
		AssertColorEquals(Color.FromArgb("#D93025"), viewModel.ChartLineColor);
	}

	[Fact]
	public async Task SelectedRangeProperties_UpdateHasSelectedRange()
	{
		var viewModel = new ExchangeViewModel(CreateService());
		await viewModel.InitializeAsync();

		var first = viewModel.HistoricalPoints[0];
		var last = viewModel.HistoricalPoints[^1];

		viewModel.SelectedRangeStart = first;
		viewModel.SelectedRangeEnd = first;
		Assert.False(viewModel.HasSelectedRange);

		viewModel.SelectedRangeEnd = last;
		Assert.True(viewModel.HasSelectedRange);
	}

	[Fact]
	public async Task InitializeAsync_WhenServiceThrows_SetsFriendlyError()
	{
		var service = CreateService();
		service.LatestRateException = new InvalidOperationException("Frankfurter rejected the request.");
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();

		Assert.True(viewModel.HasError);
		Assert.Equal("Frankfurter rejected the request.", viewModel.ErrorMessage);
		Assert.False(viewModel.IsBusy);
	}

	private static TrackingExchangeRatesService CreateService(
		decimal latestRate = 1.25m,
		IReadOnlyList<HistoricalRatePoint>? history = null)
	{
		return new TrackingExchangeRatesService
		{
			Currencies =
			[
				new CurrencyOption { Code = "JPY", Name = "Japanese Yen" },
				new CurrencyOption { Code = "USD", Name = "United States Dollar" },
				new CurrencyOption { Code = "EUR", Name = "Euro" }
			],
			LatestSnapshot = new ExchangeRateSnapshot
			{
				BaseCurrency = "USD",
				QuoteCurrency = "EUR",
				Date = new DateOnly(2026, 1, 15),
				Rate = latestRate
			},
			History = history ??
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 1.10m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 11), Rate = 1.20m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 12), Rate = 1.25m }
			]
		};
	}

	private static async Task WaitForConditionAsync(Func<bool> predicate, int timeoutMilliseconds = 2000)
	{
		var started = Environment.TickCount64;
		while (!predicate())
		{
			if (Environment.TickCount64 - started > timeoutMilliseconds)
			{
				throw new TimeoutException("The expected condition was not reached.");
			}

			await Task.Delay(25);
		}
	}

	private static void AssertColorEquals(Color expected, Color actual)
	{
		Assert.Equal(expected.Red, actual.Red, 3);
		Assert.Equal(expected.Green, actual.Green, 3);
		Assert.Equal(expected.Blue, actual.Blue, 3);
		Assert.Equal(expected.Alpha, actual.Alpha, 3);
	}

	private sealed class TrackingExchangeRatesService : IExchangeRatesService
	{
		public IReadOnlyList<CurrencyOption> Currencies { get; set; } = [];
		public ExchangeRateSnapshot LatestSnapshot { get; set; } = new()
		{
			BaseCurrency = "USD",
			QuoteCurrency = "EUR",
			Date = new DateOnly(2026, 1, 1),
			Rate = 1m
		};
		public IReadOnlyList<HistoricalRatePoint> History { get; set; } = [];
		public Exception? LatestRateException { get; set; }
		public List<TimeRangeKind> HistoryRequests { get; } = [];

		public Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Currencies);
		}

		public Task<ExchangeRateSnapshot> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
		{
			if (LatestRateException is not null)
			{
				throw LatestRateException;
			}

			return Task.FromResult(LatestSnapshot);
		}

		public Task<IReadOnlyList<HistoricalRatePoint>> GetHistoricalRatesAsync(
			string baseCurrency,
			string quoteCurrency,
			TimeRangeKind range,
			CancellationToken cancellationToken = default)
		{
			HistoryRequests.Add(range);
			return Task.FromResult(History);
		}
	}
}
