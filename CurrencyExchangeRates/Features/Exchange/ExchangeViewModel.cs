using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using CurrencyExchangeRates.Infrastructure;
using CurrencyExchangeRates.Models;
using CurrencyExchangeRates.Services;
using Microsoft.Maui.Graphics;

namespace CurrencyExchangeRates.Features.Exchange;

public sealed class ExchangeViewModel : ObservableObject
{
	private readonly IExchangeRatesService exchangeRatesService;
	private readonly Color chartNegativeFillBottomColor = Color.FromArgb("#05D93025");
	private readonly Color chartNegativeFillTopColor = Color.FromArgb("#33D93025");
	private readonly Color chartNegativeLineColor = Color.FromArgb("#D93025");
	private readonly Color chartNeutralFillBottomColor = Color.FromArgb("#056E6E6E");
	private readonly Color chartNeutralFillTopColor = Color.FromArgb("#226E6E6E");
	private readonly Color chartNeutralLineColor = Color.FromArgb("#5F6368");
	private readonly Color chartPositiveFillBottomColor = Color.FromArgb("#05188038");
	private readonly Color chartPositiveFillTopColor = Color.FromArgb("#33188038");
	private readonly Color chartPositiveLineColor = Color.FromArgb("#188038");
	private readonly Color negativeChangeColor = Color.FromArgb("#C62828");
	private readonly Color neutralChangeColor = Color.FromArgb("#6E6E6E");
	private readonly Color positiveChangeColor = Color.FromArgb("#2E7D32");
	private decimal baseAmount = 1m;
	private string baseAmountText = "1";
	private ExchangeRateSnapshot? currentRate;
	private string errorMessage = string.Empty;
	private HistoricalRatePoint? highlightedPoint;
	private bool isBusy;
	private bool isInitialized;
	private bool isLastEditedAmountBase = true;
	private bool isSelectionChangeSuppressed;
	private bool isSyncingAmounts;
	private decimal quoteAmount;
	private string quoteAmountText = "0.00";
	private HistoricalRatePoint? selectedRangeEnd;
	private HistoricalRatePoint? selectedRangeStart;
	private CurrencyOption? selectedBaseCurrency;
	private CurrencyOption? selectedQuoteCurrency;
	private TimeRangeOption? selectedTimeRange;

	public ExchangeViewModel(IExchangeRatesService exchangeRatesService)
	{
		this.exchangeRatesService = exchangeRatesService;

		TimeRanges =
		[
			new TimeRangeOption { Label = "1D", Kind = TimeRangeKind.OneDay },
			new TimeRangeOption { Label = "5D", Kind = TimeRangeKind.FiveDays },
			new TimeRangeOption { Label = "1M", Kind = TimeRangeKind.OneMonth, IsSelected = true },
			new TimeRangeOption { Label = "1Y", Kind = TimeRangeKind.OneYear },
			new TimeRangeOption { Label = "5Y", Kind = TimeRangeKind.FiveYears },
			new TimeRangeOption { Label = "Max", Kind = TimeRangeKind.Max }
		];

		selectedTimeRange = TimeRanges.First(range => range.IsSelected);

		SelectTimeRangeCommand = new Command<TimeRangeOption>(range => _ = SelectTimeRangeAsync(range));
		SwapCurrenciesCommand = new Command(async () => await SwapCurrenciesAsync(), CanSwapCurrencies);
		RetryCommand = new Command(async () => await LoadExchangeDataAsync(), CanLoadData);
	}

	public ObservableCollection<CurrencyOption> Currencies { get; } = [];

	public ObservableCollection<HistoricalRatePoint> HistoricalPoints { get; } = [];

	public ObservableCollection<TimeRangeOption> TimeRanges { get; }

	public Command<TimeRangeOption> SelectTimeRangeCommand { get; }

	public Command SwapCurrenciesCommand { get; }

	public Command RetryCommand { get; }

	public string BaseAmountText
	{
		get => baseAmountText;
		set
		{
			if (!SetProperty(ref baseAmountText, value))
			{
				return;
			}

			if (isSyncingAmounts || !TryParseAmount(value, out var parsedAmount))
			{
				return;
			}

			baseAmount = parsedAmount;
			isLastEditedAmountBase = true;
			SyncAmountsFromRate();
		}
	}

	public string QuoteAmountText
	{
		get => quoteAmountText;
		set
		{
			if (!SetProperty(ref quoteAmountText, value))
			{
				return;
			}

			if (isSyncingAmounts || !TryParseAmount(value, out var parsedAmount))
			{
				return;
			}

			quoteAmount = parsedAmount;
			isLastEditedAmountBase = false;
			SyncAmountsFromRate();
		}
	}

	public CurrencyOption? SelectedBaseCurrency
	{
		get => selectedBaseCurrency;
		set
		{
			if (!SetProperty(ref selectedBaseCurrency, value))
			{
				return;
			}

			RaiseDisplayProperties();
			SwapCurrenciesCommand.ChangeCanExecute();
			RetryCommand.ChangeCanExecute();
			TriggerSelectionRefresh();
		}
	}

	public CurrencyOption? SelectedQuoteCurrency
	{
		get => selectedQuoteCurrency;
		set
		{
			if (!SetProperty(ref selectedQuoteCurrency, value))
			{
				return;
			}

			RaiseDisplayProperties();
			SwapCurrenciesCommand.ChangeCanExecute();
			RetryCommand.ChangeCanExecute();
			TriggerSelectionRefresh();
		}
	}

	public TimeRangeOption? SelectedTimeRange
	{
		get => selectedTimeRange;
		private set
		{
			if (!SetProperty(ref selectedTimeRange, value))
			{
				return;
			}

			OnPropertyChanged(nameof(CurrentRangeText));
		}
	}

	public HistoricalRatePoint? HighlightedPoint
	{
		get => highlightedPoint;
		set
		{
			if (!SetProperty(ref highlightedPoint, value))
			{
				return;
			}

			RaiseDisplayProperties();
		}
	}

	public HistoricalRatePoint? SelectedRangeStart
	{
		get => selectedRangeStart;
		set
		{
			if (!SetProperty(ref selectedRangeStart, value))
			{
				return;
			}

			RaiseDisplayProperties();
		}
	}

	public HistoricalRatePoint? SelectedRangeEnd
	{
		get => selectedRangeEnd;
		set
		{
			if (!SetProperty(ref selectedRangeEnd, value))
			{
				return;
			}

			RaiseDisplayProperties();
		}
	}

	public bool IsBusy
	{
		get => isBusy;
		private set
		{
			if (!SetProperty(ref isBusy, value))
			{
				return;
			}

			OnPropertyChanged(nameof(IsReady));
		}
	}

	public bool IsReady => !IsBusy;

	public string ErrorMessage
	{
		get => errorMessage;
		private set
		{
			if (!SetProperty(ref errorMessage, value))
			{
				return;
			}

			OnPropertyChanged(nameof(HasError));
		}
	}

	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public bool IsChartEmpty => HistoricalPoints.Count == 0;

	public bool HasSelectedRange =>
		SelectedRangeStart is not null &&
		SelectedRangeEnd is not null &&
		!SelectedRangeStart.Equals(SelectedRangeEnd);

	public string DisplayRateText => FormatAmountForDisplay(currentRate?.Rate ?? 0m);

	public string DisplayBaseSummaryText => SelectedBaseCurrency is null
		? "Select a currency pair"
		: $"1 {SelectedBaseCurrency.Name} equals";

	public string DisplayQuoteNameText => SelectedQuoteCurrency?.Name ?? string.Empty;

	public string DisplayPairText
	{
		get
		{
			var baseCode = SelectedBaseCurrency?.Code ?? "---";
			var quoteCode = SelectedQuoteCurrency?.Code ?? "---";
			return $"1 {baseCode} = {DisplayRateText} {quoteCode}";
		}
	}

	public string DisplayDateText
	{
		get
		{
			return currentRate is null
				? "Select two currencies to load live and historical rates."
				: $"Updated {currentRate.Date:MMM d, yyyy}";
		}
	}

	public bool HasDisplayTrend => false;

	public string DisplayTrendText => string.Empty;

	public Color DisplayTrendColor => neutralChangeColor;

	public string ChangeSummaryText
	{
		get
		{
			if (HistoricalPoints.Count < 2)
			{
				return "Not enough history yet for a range comparison.";
			}

			var baseline = HistoricalPoints[0].Rate;
			var current = HistoricalPoints[^1].Rate;
			var delta = current - baseline;
			var percent = baseline == 0m ? 0m : delta / baseline * 100m;
			var sign = delta >= 0 ? "+" : string.Empty;
			return $"{sign}{delta:N4} ({sign}{percent:N2}%) · {CurrentRangeText}";
		}
	}

	public Color ChangeSummaryColor
	{
		get
		{
			if (HistoricalPoints.Count < 2)
			{
				return neutralChangeColor;
			}

			var baseline = HistoricalPoints[0].Rate;
			var current = HistoricalPoints[^1].Rate;

			if (current > baseline)
			{
				return positiveChangeColor;
			}

			if (current < baseline)
			{
				return negativeChangeColor;
			}

			return neutralChangeColor;
		}
	}

	public Color ChartLineColor => GetWindowTrendDirection() switch
	{
		> 0 => chartPositiveLineColor,
		< 0 => chartNegativeLineColor,
		_ => chartNeutralLineColor
	};

	public Color ChartFillTopColor => GetWindowTrendDirection() switch
	{
		> 0 => chartPositiveFillTopColor,
		< 0 => chartNegativeFillTopColor,
		_ => chartNeutralFillTopColor
	};

	public Color ChartFillBottomColor => GetWindowTrendDirection() switch
	{
		> 0 => chartPositiveFillBottomColor,
		< 0 => chartNegativeFillBottomColor,
		_ => chartNeutralFillBottomColor
	};

	public string CurrentRangeText => SelectedTimeRange?.Label ?? string.Empty;

	public string DataSourceText => $"Frankfurter API · {GetRangeDetailText()}";

	public async Task InitializeAsync()
	{
		if (isInitialized)
		{
			return;
		}

		await LoadCurrenciesAsync();
		isInitialized = true;
		await LoadExchangeDataAsync();
	}

	private async Task LoadCurrenciesAsync()
	{
		var currencies = await exchangeRatesService.GetCurrenciesAsync();

		Currencies.Clear();
		foreach (var currency in currencies)
		{
			Currencies.Add(currency);
		}

		isSelectionChangeSuppressed = true;

		SelectedBaseCurrency = Currencies.FirstOrDefault(currency => currency.Code == "USD") ?? Currencies.FirstOrDefault();
		SelectedQuoteCurrency = Currencies.FirstOrDefault(currency => currency.Code == "EUR")
			?? Currencies.FirstOrDefault(currency => currency.Code != SelectedBaseCurrency?.Code)
			?? Currencies.FirstOrDefault();

		isSelectionChangeSuppressed = false;
	}

	private async Task LoadExchangeDataAsync()
	{
		if (SelectedBaseCurrency is null || SelectedQuoteCurrency is null)
		{
			return;
		}

		IsBusy = true;
		ErrorMessage = string.Empty;
		RetryCommand.ChangeCanExecute();
		SwapCurrenciesCommand.ChangeCanExecute();

		try
		{
			var latestRateTask = exchangeRatesService.GetLatestRateAsync(SelectedBaseCurrency.Code, SelectedQuoteCurrency.Code);
			var historyTask = exchangeRatesService.GetHistoricalRatesAsync(
				SelectedBaseCurrency.Code,
				SelectedQuoteCurrency.Code,
				SelectedTimeRange?.Kind ?? TimeRangeKind.OneMonth);

			await Task.WhenAll(latestRateTask, historyTask);

			currentRate = await latestRateTask;
			SyncAmountsFromRate();
			ReplaceHistory(await historyTask);
		}
		catch (HttpRequestException exception)
		{
			ErrorMessage = $"Could not reach Frankfurter: {exception.Message}";
		}
		catch (InvalidOperationException exception)
		{
			ErrorMessage = exception.Message;
		}
		catch (JsonException exception)
		{
			ErrorMessage = $"Frankfurter returned invalid data: {exception.Message}";
		}
		finally
		{
			IsBusy = false;
			RetryCommand.ChangeCanExecute();
			SwapCurrenciesCommand.ChangeCanExecute();
			RaiseDisplayProperties();
		}
	}

	private void ReplaceHistory(IEnumerable<HistoricalRatePoint> history)
	{
		HistoricalPoints.Clear();
		SelectedRangeStart = null;
		SelectedRangeEnd = null;

		foreach (var point in history)
		{
			HistoricalPoints.Add(point);
		}

		HighlightedPoint = HistoricalPoints.LastOrDefault();
		OnPropertyChanged(nameof(IsChartEmpty));
	}

	private async Task SelectTimeRangeAsync(TimeRangeOption? range)
	{
		if (range is null || SelectedTimeRange == range)
		{
			return;
		}

		foreach (var option in TimeRanges)
		{
			option.IsSelected = ReferenceEquals(option, range);
		}

		SelectedTimeRange = range;

		if (isInitialized)
		{
			await LoadExchangeDataAsync();
		}
	}

	private async Task SwapCurrenciesAsync()
	{
		if (!CanSwapCurrencies())
		{
			return;
		}

		isSelectionChangeSuppressed = true;
		(SelectedBaseCurrency, SelectedQuoteCurrency) = (SelectedQuoteCurrency, SelectedBaseCurrency);
		isSelectionChangeSuppressed = false;

		await LoadExchangeDataAsync();
	}

	private bool CanSwapCurrencies()
	{
		return !IsBusy && SelectedBaseCurrency is not null && SelectedQuoteCurrency is not null;
	}

	private bool CanLoadData()
	{
		return !IsBusy && SelectedBaseCurrency is not null && SelectedQuoteCurrency is not null;
	}

	private void TriggerSelectionRefresh()
	{
		if (!isInitialized || isSelectionChangeSuppressed)
		{
			return;
		}

		_ = LoadExchangeDataAsync();
	}

	private string GetRangeDetailText()
	{
		return SelectedTimeRange?.Kind switch
		{
			TimeRangeKind.OneDay => "daily latest vs previous day",
			TimeRangeKind.FiveDays => "last 5 days",
			TimeRangeKind.OneMonth => "last 1 month",
			TimeRangeKind.OneYear => "last 1 year · weekly grouped",
			TimeRangeKind.FiveYears => "last 5 years · monthly grouped",
			TimeRangeKind.Max => "full history · monthly grouped",
			_ => "daily rates"
		};
	}

	private int GetWindowTrendDirection()
	{
		if (HistoricalPoints.Count < 2)
		{
			return 0;
		}

		var delta = HistoricalPoints[^1].Rate - HistoricalPoints[0].Rate;
		return delta switch
		{
			> 0m => 1,
			< 0m => -1,
			_ => 0
		};
	}

	private (HistoricalRatePoint Start, HistoricalRatePoint End) GetOrderedSelectedRange()
	{
		if (SelectedRangeStart is null || SelectedRangeEnd is null)
		{
			throw new InvalidOperationException("Selected range is not available.");
		}

		return SelectedRangeStart.Date <= SelectedRangeEnd.Date
			? (SelectedRangeStart, SelectedRangeEnd)
			: (SelectedRangeEnd, SelectedRangeStart);
	}

	private Color GetTrendColor(decimal startRate, decimal endRate)
	{
		if (endRate > startRate)
		{
			return positiveChangeColor;
		}

		if (endRate < startRate)
		{
			return negativeChangeColor;
		}

		return neutralChangeColor;
	}

	private void SyncAmountsFromRate()
	{
		if (currentRate is null)
		{
			return;
		}

		if (isLastEditedAmountBase || currentRate.Rate == 0m)
		{
			quoteAmount = baseAmount * currentRate.Rate;
		}
		else
		{
			baseAmount = quoteAmount / currentRate.Rate;
		}

		isSyncingAmounts = true;
		BaseAmountText = FormatAmountForInput(baseAmount);
		QuoteAmountText = FormatAmountForInput(quoteAmount);
		isSyncingAmounts = false;
	}

	private static string FormatAmountForDisplay(decimal value)
	{
		return value >= 100m ? value.ToString("N2", CultureInfo.CurrentCulture) : value.ToString("N4", CultureInfo.CurrentCulture);
	}

	private static string FormatAmountForInput(decimal value)
	{
		return decimal.Round(value, 4).ToString("0.####", CultureInfo.CurrentCulture);
	}

	private static bool TryParseAmount(string? value, out decimal amount)
	{
		return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) ||
			decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
	}

	private void RaiseDisplayProperties()
	{
		OnPropertyChanged(nameof(HasSelectedRange));
		OnPropertyChanged(nameof(DisplayBaseSummaryText));
		OnPropertyChanged(nameof(DisplayRateText));
		OnPropertyChanged(nameof(DisplayQuoteNameText));
		OnPropertyChanged(nameof(DisplayPairText));
		OnPropertyChanged(nameof(DisplayDateText));
		OnPropertyChanged(nameof(HasDisplayTrend));
		OnPropertyChanged(nameof(DisplayTrendText));
		OnPropertyChanged(nameof(DisplayTrendColor));
		OnPropertyChanged(nameof(ChangeSummaryText));
		OnPropertyChanged(nameof(ChangeSummaryColor));
		OnPropertyChanged(nameof(ChartLineColor));
		OnPropertyChanged(nameof(ChartFillTopColor));
		OnPropertyChanged(nameof(ChartFillBottomColor));
		OnPropertyChanged(nameof(DataSourceText));
	}
}
