using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CurrencyExchangeRates.Models;

namespace CurrencyExchangeRates.Services;

public sealed class FrankfurterApiClient(HttpClient httpClient) : IFrankfurterApiClient
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public async Task<IReadOnlyList<CurrencyOption>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
	{
		var response = await GetAsync<List<CurrencyResponse>>("currencies", cancellationToken);

		return response
			.Where(currency => !string.IsNullOrWhiteSpace(currency.IsoCode) && !string.IsNullOrWhiteSpace(currency.Name))
			.Select(currency => new CurrencyOption
			{
				Code = currency.IsoCode,
				Name = currency.Name
			})
			.OrderBy(currency => currency.Code, StringComparer.Ordinal)
			.ToArray();
	}

	public async Task<ExchangeRateSnapshot> GetLatestRateAsync(
		string baseCurrency,
		string quoteCurrency,
		CancellationToken cancellationToken = default)
	{
		var response = await GetAsync<RateResponse>(
			$"rate/{Uri.EscapeDataString(baseCurrency)}/{Uri.EscapeDataString(quoteCurrency)}",
			cancellationToken);

		return new ExchangeRateSnapshot
		{
			BaseCurrency = response.BaseCurrency,
			QuoteCurrency = response.QuoteCurrency,
			Date = ParseDate(response.Date),
			Rate = response.Rate
		};
	}

	public async Task<IReadOnlyList<HistoricalRatePoint>> GetHistoricalRatesAsync(
		string baseCurrency,
		string quoteCurrency,
		DateOnly fromDate,
		string? groupBy,
		CancellationToken cancellationToken = default)
	{
		var queryParts = new List<string>
		{
			$"base={Uri.EscapeDataString(baseCurrency)}",
			$"quotes={Uri.EscapeDataString(quoteCurrency)}",
			$"from={Uri.EscapeDataString(fromDate.ToString("yyyy-MM-dd"))}"
		};

		if (!string.IsNullOrWhiteSpace(groupBy))
		{
			queryParts.Add($"group={Uri.EscapeDataString(groupBy)}");
		}

		var response = await GetAsync<List<RateResponse>>(
			$"rates?{string.Join("&", queryParts)}",
			cancellationToken);

		return response
			.Select(rate => new HistoricalRatePoint
			{
				Date = ParseDate(rate.Date),
				Rate = rate.Rate
			})
			.OrderBy(point => point.Date)
			.ToArray();
	}

	private async Task<T> GetAsync<T>(string relativeUri, CancellationToken cancellationToken)
	{
		using var response = await httpClient.GetAsync(relativeUri, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(await BuildErrorMessageAsync(response, cancellationToken));
		}

		try
		{
			var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
			return payload ?? throw new InvalidOperationException("Frankfurter returned an empty response.");
		}
		catch (JsonException exception)
		{
			throw new InvalidOperationException("Frankfurter returned data in an unexpected format.", exception);
		}
	}

	private static async Task<string> BuildErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		try
		{
			var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, cancellationToken);
			if (!string.IsNullOrWhiteSpace(error?.Message))
			{
				return $"Frankfurter error {(int)response.StatusCode}: {error.Message}";
			}
		}
		catch (JsonException)
		{
		}

		return response.StatusCode switch
		{
			HttpStatusCode.BadRequest => "Frankfurter rejected the request parameters.",
			HttpStatusCode.NotFound => "Frankfurter could not find the requested currency pair.",
			HttpStatusCode.UnprocessableEntity => "Frankfurter could not process the requested date range.",
			_ => $"Frankfurter request failed with status code {(int)response.StatusCode}."
		};
	}

	private static DateOnly ParseDate(string value)
	{
		if (!DateOnly.TryParse(value, out var date))
		{
			throw new InvalidOperationException($"Frankfurter returned an invalid date: {value}");
		}

		return date;
	}

	private sealed class CurrencyResponse
	{
		[JsonPropertyName("iso_code")]
		public string IsoCode { get; set; } = string.Empty;

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;
	}

	private sealed class RateResponse
	{
		[JsonPropertyName("date")]
		public string Date { get; set; } = string.Empty;

		[JsonPropertyName("base")]
		public string BaseCurrency { get; set; } = string.Empty;

		[JsonPropertyName("quote")]
		public string QuoteCurrency { get; set; } = string.Empty;

		[JsonPropertyName("rate")]
		public decimal Rate { get; set; }
	}

	private sealed class ErrorResponse
	{
		[JsonPropertyName("message")]
		public string Message { get; set; } = string.Empty;
	}
}
