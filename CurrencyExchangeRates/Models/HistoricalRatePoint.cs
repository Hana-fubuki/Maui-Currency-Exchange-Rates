namespace CurrencyExchangeRates.Models;

public sealed class HistoricalRatePoint : IEquatable<HistoricalRatePoint>
{
	public required DateOnly Date { get; init; }

	public required decimal Rate { get; init; }

	public bool Equals(HistoricalRatePoint? other)
	{
		return other is not null && Date == other.Date && Rate == other.Rate;
	}

	public override bool Equals(object? obj)
	{
		return obj is HistoricalRatePoint other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Date, Rate);
	}
}
