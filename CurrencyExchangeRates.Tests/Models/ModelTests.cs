using CurrencyExchangeRates.Models;

namespace CurrencyExchangeRates.Tests.Models;

public sealed class ModelTests
{
	[Fact]
	public void CurrencyOption_DisplayName_CombinesCodeAndName()
	{
		var option = new CurrencyOption
		{
			Code = "USD",
			Name = "United States Dollar"
		};

		Assert.Equal("USD - United States Dollar", option.DisplayName);
	}

	[Fact]
	public void HistoricalRatePoint_Equality_UsesDateAndRate()
	{
		var left = new HistoricalRatePoint { Date = new DateOnly(2026, 1, 1), Rate = 1.25m };
		var same = new HistoricalRatePoint { Date = new DateOnly(2026, 1, 1), Rate = 1.25m };
		var different = new HistoricalRatePoint { Date = new DateOnly(2026, 1, 2), Rate = 1.25m };

		Assert.Equal(left, same);
		Assert.NotEqual(left, different);
		Assert.Equal(left.GetHashCode(), same.GetHashCode());
	}
}
