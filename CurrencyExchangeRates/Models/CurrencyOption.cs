namespace CurrencyExchangeRates.Models;

public sealed class CurrencyOption
{
	public required string Code { get; init; }

	public required string Name { get; init; }

	public string DisplayName => $"{Code} - {Name}";
}
