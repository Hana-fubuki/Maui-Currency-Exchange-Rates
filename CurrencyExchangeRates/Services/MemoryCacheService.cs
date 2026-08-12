namespace CurrencyExchangeRates.Services;

public sealed class MemoryCacheService : ICacheService
{
	private readonly Dictionary<string, CacheEntry> cache = new();
	private readonly Lock cacheLock = new();

	public bool TryGetValue<T>(string key, out T? value)
	{
		lock (cacheLock)
		{
			if (!cache.TryGetValue(key, out var entry))
			{
				value = default;
				return false;
			}

			if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
			{
				cache.Remove(key);
				value = default;
				return false;
			}

			if (entry.Value is T typedValue)
			{
				value = typedValue;
				return true;
			}
		}

		value = default;
		return false;
	}

	public void Set<T>(string key, T value, TimeSpan ttl)
	{
		lock (cacheLock)
		{
			cache[key] = new CacheEntry(value!, DateTimeOffset.UtcNow.Add(ttl));
		}
	}

	private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);
}
