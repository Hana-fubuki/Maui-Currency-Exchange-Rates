using CurrencyExchangeRates.Infrastructure;

namespace CurrencyExchangeRates.Models;

public sealed class TimeRangeOption : ObservableObject
{
	private bool isSelected;

	public required string Label { get; init; }

	public required TimeRangeKind Kind { get; init; }

	public bool IsSelected
	{
		get => isSelected;
		set => SetProperty(ref isSelected, value);
	}
}
