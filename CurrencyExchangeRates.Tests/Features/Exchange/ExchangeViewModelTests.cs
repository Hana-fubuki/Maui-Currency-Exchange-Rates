using System.Net.Http;
using System.Text.Json;
using CurrencyExchangeRates.Features.Exchange;
using CurrencyExchangeRates.Models;
using CurrencyExchangeRates.Services;
using Microsoft.Maui.Graphics;

namespace CurrencyExchangeRates.Tests.Features.Exchange;

public sealed class ExchangeViewModelTests
{
	[Fact]
	public void InitialState_ShowsNeutralPlaceholders_AndDisabledCommands()
	{
		var viewModel = new ExchangeViewModel(CreateService());

		Assert.Equal("Select a currency pair", viewModel.DisplayBaseSummaryText);
		Assert.Equal("1 --- = 0.0000 ---", viewModel.DisplayPairText);
		Assert.Equal("Select two currencies to load live and historical rates.", viewModel.DisplayDateText);
		Assert.False(viewModel.HasDisplayTrend);
		Assert.Equal(string.Empty, viewModel.DisplayTrendText);
		Assert.Equal("Not enough history yet for a range comparison.", viewModel.ChangeSummaryText);
		AssertColorEquals(Color.FromArgb("#6E6E6E"), viewModel.ChangeSummaryColor);
		AssertColorEquals(Color.FromArgb("#5F6368"), viewModel.ChartLineColor);
		AssertColorEquals(Color.FromArgb("#226E6E6E"), viewModel.ChartFillTopColor);
		AssertColorEquals(Color.FromArgb("#056E6E6E"), viewModel.ChartFillBottomColor);
		Assert.Equal("Frankfurter API · last 1 month", viewModel.DataSourceText);
		Assert.False(viewModel.SwapCurrenciesCommand.CanExecute(null));
		Assert.False(viewModel.RetryCommand.CanExecute(null));
	}

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
	public async Task InitializeAsync_WhenCalledTwice_LoadsDataOnlyOnce()
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();
		await viewModel.InitializeAsync();

		Assert.Equal(1, service.GetCurrenciesCallCount);
		Assert.Single(service.HistoryRequests);
		Assert.Single(service.LatestRateRequests);
	}

	[Fact]
	public async Task InitializeAsync_UsesFallbackCurrencies_WhenPreferredPairIsUnavailable()
	{
		var service = CreateService();
		service.Currencies =
		[
			new CurrencyOption { Code = "JPY", Name = "Japanese Yen" },
			new CurrencyOption { Code = "GBP", Name = "British Pound Sterling" }
		];
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();

		Assert.Equal("JPY", viewModel.SelectedBaseCurrency?.Code);
		Assert.Equal("GBP", viewModel.SelectedQuoteCurrency?.Code);
		Assert.Equal(("JPY", "GBP"), service.LatestRateRequests.Single());
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
	public async Task BaseAmountText_IgnoresInvalidInput()
	{
		var viewModel = new ExchangeViewModel(CreateService(latestRate: 2m));
		await viewModel.InitializeAsync();

		viewModel.BaseAmountText = "invalid";

		Assert.Equal("invalid", viewModel.BaseAmountText);
		Assert.Equal("2", viewModel.QuoteAmountText);
	}

	[Fact]
	public void BaseAmountText_BeforeInitialization_DoesNotTryToCalculate()
	{
		var viewModel = new ExchangeViewModel(CreateService());

		viewModel.BaseAmountText = "2";

		Assert.Equal("2", viewModel.BaseAmountText);
		Assert.Equal("0.00", viewModel.QuoteAmountText);
	}

	[Fact]
	public async Task QuoteAmountText_WhenRateIsZero_KeepsBaseAmountStable()
	{
		var viewModel = new ExchangeViewModel(CreateService(latestRate: 0m));
		await viewModel.InitializeAsync();

		viewModel.QuoteAmountText = "10";

		Assert.Equal("1", viewModel.BaseAmountText);
		Assert.Equal("0", viewModel.QuoteAmountText);
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
	public async Task SelectedBaseCurrency_AfterInitialization_ReloadsData()
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);
		await viewModel.InitializeAsync();

		viewModel.SelectedBaseCurrency = viewModel.Currencies.Single(currency => currency.Code == "JPY");
		await WaitForConditionAsync(() => service.LatestRateRequests.Count >= 2);

		Assert.Equal(("JPY", "EUR"), service.LatestRateRequests[^1]);
		Assert.Equal(TimeRangeKind.OneMonth, service.HistoryRequests[^1]);
	}

	[Fact]
	public async Task SwapCurrenciesCommand_SwapsPair_AndReloadsData()
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);
		await viewModel.InitializeAsync();

		viewModel.SwapCurrenciesCommand.Execute(null);
		await WaitForConditionAsync(() => service.LatestRateRequests.Count >= 2);

		Assert.Equal("EUR", viewModel.SelectedBaseCurrency?.Code);
		Assert.Equal("USD", viewModel.SelectedQuoteCurrency?.Code);
		Assert.Equal(("EUR", "USD"), service.LatestRateRequests[^1]);
	}

	[Fact]
	public async Task RetryCommand_ReloadsCurrentSelection()
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);
		await viewModel.InitializeAsync();

		viewModel.RetryCommand.Execute(null);
		await WaitForConditionAsync(() => service.LatestRateRequests.Count >= 2);

		Assert.Equal(("USD", "EUR"), service.LatestRateRequests[^1]);
		Assert.Equal(2, service.HistoryRequests.Count);
	}

	[Theory]
	[InlineData(TimeRangeKind.OneDay, "1D", "Frankfurter API · daily latest vs previous day")]
	[InlineData(TimeRangeKind.FiveDays, "5D", "Frankfurter API · last 5 days")]
	[InlineData(TimeRangeKind.OneMonth, "1M", "Frankfurter API · last 1 month")]
	[InlineData(TimeRangeKind.OneYear, "1Y", "Frankfurter API · last 1 year · weekly grouped")]
	[InlineData(TimeRangeKind.FiveYears, "5Y", "Frankfurter API · last 5 years · monthly grouped")]
	[InlineData(TimeRangeKind.Max, "Max", "Frankfurter API · full history · monthly grouped")]
	public async Task DataSourceText_ReflectsSelectedRange(TimeRangeKind range, string expectedLabel, string expectedText)
	{
		var service = CreateService();
		var viewModel = new ExchangeViewModel(service);
		await viewModel.InitializeAsync();

		if (range != TimeRangeKind.OneMonth)
		{
			var targetRange = viewModel.TimeRanges.Single(option => option.Kind == range);
			viewModel.SelectTimeRangeCommand.Execute(targetRange);
			await WaitForConditionAsync(() => service.HistoryRequests.Count >= 2);
		}

		Assert.Equal(expectedLabel, viewModel.CurrentRangeText);
		Assert.Equal(expectedText, viewModel.DataSourceText);
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
		AssertColorEquals(Color.FromArgb("#33188038"), viewModel.ChartFillTopColor);
		AssertColorEquals(Color.FromArgb("#05188038"), viewModel.ChartFillBottomColor);
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
		AssertColorEquals(Color.FromArgb("#33D93025"), viewModel.ChartFillTopColor);
		AssertColorEquals(Color.FromArgb("#05D93025"), viewModel.ChartFillBottomColor);
	}

	[Fact]
	public async Task ChangeSummary_AndChartColors_ReflectNeutralTrend()
	{
		var viewModel = new ExchangeViewModel(CreateService(
			history:
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 1.00m },
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 11), Rate = 1.00m }
			]));
		await viewModel.InitializeAsync();

		Assert.StartsWith("+0.0000 (+0.00%)", viewModel.ChangeSummaryText);
		AssertColorEquals(Color.FromArgb("#6E6E6E"), viewModel.ChangeSummaryColor);
		AssertColorEquals(Color.FromArgb("#5F6368"), viewModel.ChartLineColor);
		AssertColorEquals(Color.FromArgb("#226E6E6E"), viewModel.ChartFillTopColor);
		AssertColorEquals(Color.FromArgb("#056E6E6E"), viewModel.ChartFillBottomColor);
	}

	[Fact]
	public async Task ChangeSummary_ShowsFallback_WhenHistoryHasSinglePoint()
	{
		var viewModel = new ExchangeViewModel(CreateService(
			history:
			[
				new HistoricalRatePoint { Date = new DateOnly(2026, 1, 10), Rate = 1.00m }
			]));
		await viewModel.InitializeAsync();

		Assert.Equal("Not enough history yet for a range comparison.", viewModel.ChangeSummaryText);
		AssertColorEquals(Color.FromArgb("#6E6E6E"), viewModel.ChangeSummaryColor);
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

	[Fact]
	public async Task InitializeAsync_WhenServiceThrowsHttpRequestException_SetsFriendlyError()
	{
		var service = CreateService();
		service.LatestRateException = new HttpRequestException("offline");
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();

		Assert.Equal("Could not reach Frankfurter: offline", viewModel.ErrorMessage);
	}

	[Fact]
	public async Task InitializeAsync_WhenServiceThrowsJsonException_SetsFriendlyError()
	{
		var service = CreateService();
		service.LatestRateException = new JsonException("bad payload");
		var viewModel = new ExchangeViewModel(service);

		await viewModel.InitializeAsync();

		Assert.Equal("Frankfurter returned invalid data: bad payload", viewModel.ErrorMessage);
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
		public int GetCurrenciesCallCount { get; private set; }
		public List<(string BaseCurrency, string QuoteCurrency)> LatestRateRequests { get; } = [];
		public Exception? LatestRateException { get; set; }
		public Exception? CurrenciesException { get; set; }
		public Exception? HistoryException { get; set; }
		public List<TimeRangeKind> HistoryRequests { get; } = [];

		public Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
		{
			GetCurrenciesCallCount++;
			if (CurrenciesException is not null)
			{
				throw CurrenciesException;
			}

			return Task.FromResult(Currencies);
		}

		public Task<ExchangeRateSnapshot> GetLatestRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
		{
			LatestRateRequests.Add((baseCurrency, quoteCurrency));
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
			if (HistoryException is not null)
			{
				throw HistoryException;
			}

			HistoryRequests.Add(range);
			return Task.FromResult(History);
		}
	}
}
