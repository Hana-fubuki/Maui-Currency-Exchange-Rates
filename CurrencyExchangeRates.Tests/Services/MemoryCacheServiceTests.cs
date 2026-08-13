using CurrencyExchangeRates.Services;

namespace CurrencyExchangeRates.Tests.Services;

public sealed class MemoryCacheServiceTests
{
	[Fact]
	public void TryGetValue_ReturnsStoredValue_BeforeExpiration()
	{
		var cache = new MemoryCacheService();
		cache.Set("latest:USD:EUR", 1.08m, TimeSpan.FromMinutes(5));

		var found = cache.TryGetValue<decimal>("latest:USD:EUR", out var value);

		Assert.True(found);
		Assert.Equal(1.08m, value);
	}

	[Fact]
	public void TryGetValue_ReturnsFalse_ForExpiredValue()
	{
		var cache = new MemoryCacheService();
		cache.Set("currencies", new[] { "USD" }, TimeSpan.FromMilliseconds(-1));

		var found = cache.TryGetValue<string[]>("currencies", out var value);

		Assert.False(found);
		Assert.Null(value);
	}

	[Fact]
	public void TryGetValue_ReturnsFalse_ForMismatchedType()
	{
		var cache = new MemoryCacheService();
		cache.Set("currencies", 42, TimeSpan.FromMinutes(5));

		var found = cache.TryGetValue<string>("currencies", out var value);

		Assert.False(found);
		Assert.Null(value);
	}
}
