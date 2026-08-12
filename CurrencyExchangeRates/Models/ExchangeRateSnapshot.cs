namespace CurrencyExchangeRates.Models;

public sealed class ExchangeRateSnapshot
{
	public required string BaseCurrency { get; init; }

	public required string QuoteCurrency { get; init; }

	public required DateOnly Date { get; init; }

	public required decimal Rate { get; init; }
}
