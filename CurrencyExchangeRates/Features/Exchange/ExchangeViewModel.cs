using System.Collections.ObjectModel;
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
	private readonly Color negativeChangeColor = Color.FromArgb("#C62828");
	private readonly Color neutralChangeColor = Color.FromArgb("#6E6E6E");
	private readonly Color positiveChangeColor = Color.FromArgb("#2E7D32");
	private ExchangeRateSnapshot? currentRate;
	private string errorMessage = string.Empty;
	private HistoricalRatePoint? highlightedPoint;
	private bool isBusy;
	private bool isInitialized;
	private bool isSelectionChangeSuppressed;
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

	public string DisplayRateText => $"{GetDisplayedRate():N4}";

	public string DisplayPairText
	{
		get
		{
			var baseCode = SelectedBaseCurrency?.Code ?? "---";
			var quoteCode = SelectedQuoteCurrency?.Code ?? "---";
			return $"1 {baseCode} = {GetDisplayedRate():N4} {quoteCode}";
		}
	}

	public string DisplayDateText
	{
		get
		{
			var point = GetDisplayPoint();
			return point is null
				? "Select two currencies to load live and historical rates."
				: $"Rate on {point.Date:MMM d, yyyy}";
		}
	}

	public string ChangeSummaryText
	{
		get
		{
			if (HistoricalPoints.Count < 2)
			{
				return "Not enough history yet for a range comparison.";
			}

			var baseline = HistoricalPoints[0].Rate;
			var current = GetDisplayedRate();
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
			var current = GetDisplayedRate();

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

	private HistoricalRatePoint? GetDisplayPoint()
	{
		if (HighlightedPoint is not null)
		{
			return HighlightedPoint;
		}

		if (currentRate is null)
		{
			return null;
		}

		return new HistoricalRatePoint
		{
			Date = currentRate.Date,
			Rate = currentRate.Rate
		};
	}

	private decimal GetDisplayedRate()
	{
		return HighlightedPoint?.Rate ?? currentRate?.Rate ?? 0m;
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

	private void RaiseDisplayProperties()
	{
		OnPropertyChanged(nameof(DisplayRateText));
		OnPropertyChanged(nameof(DisplayPairText));
		OnPropertyChanged(nameof(DisplayDateText));
		OnPropertyChanged(nameof(ChangeSummaryText));
		OnPropertyChanged(nameof(ChangeSummaryColor));
		OnPropertyChanged(nameof(DataSourceText));
	}
}
