using System.Net;
using System.Net.Http;
using System.Text;
using CurrencyExchangeRates.Services;

namespace CurrencyExchangeRates.Tests.Services;

public sealed class FrankfurterApiClientTests
{
	[Fact]
	public async Task GetCurrenciesAsync_FiltersInvalidRows_AndSortsByCode()
	{
		var client = CreateClient(_ => CreateJsonResponse("""
			[
			  { "iso_code": "JPY", "name": "Japanese Yen" },
			  { "iso_code": "", "name": "Invalid" },
			  { "iso_code": "EUR", "name": "Euro" },
			  { "iso_code": "USD", "name": "United States Dollar" },
			  { "iso_code": "AUD", "name": "" }
			]
			"""));

		var currencies = await client.GetCurrenciesAsync();

		Assert.Collection(
			currencies,
			item => Assert.Equal(("EUR", "Euro"), (item.Code, item.Name)),
			item => Assert.Equal(("JPY", "Japanese Yen"), (item.Code, item.Name)),
			item => Assert.Equal(("USD", "United States Dollar"), (item.Code, item.Name)));
	}

	[Fact]
	public async Task GetLatestRateAsync_MapsResponseIntoSnapshot()
	{
		var client = CreateClient(request =>
		{
			Assert.Equal("https://example.test/rate/USD/EUR", request.RequestUri?.ToString());
			return CreateJsonResponse("""
				{ "base": "USD", "quote": "EUR", "date": "2026-01-15", "rate": 0.9123 }
				""");
		});

		var snapshot = await client.GetLatestRateAsync("USD", "EUR");

		Assert.Equal("USD", snapshot.BaseCurrency);
		Assert.Equal("EUR", snapshot.QuoteCurrency);
		Assert.Equal(new DateOnly(2026, 1, 15), snapshot.Date);
		Assert.Equal(0.9123m, snapshot.Rate);
	}

	[Fact]
	public async Task GetHistoricalRatesAsync_SortsHistory_AndAddsGroupQuery()
	{
		var client = CreateClient(request =>
		{
			Assert.Equal("https://example.test/rates?base=USD&quotes=JPY&from=2026-01-01&group=week", request.RequestUri?.ToString());
			return CreateJsonResponse("""
				[
				  { "date": "2026-01-03", "rate": 157.3 },
				  { "date": "2026-01-01", "rate": 155.1 },
				  { "date": "2026-01-02", "rate": 156.2 }
				]
				""");
		});

		var history = await client.GetHistoricalRatesAsync("USD", "JPY", new DateOnly(2026, 1, 1), "week");

		Assert.Collection(
			history,
			item => Assert.Equal((new DateOnly(2026, 1, 1), 155.1m), (item.Date, item.Rate)),
			item => Assert.Equal((new DateOnly(2026, 1, 2), 156.2m), (item.Date, item.Rate)),
			item => Assert.Equal((new DateOnly(2026, 1, 3), 157.3m), (item.Date, item.Rate)));
	}

	[Fact]
	public async Task GetLatestRateAsync_UsesFrankfurterErrorMessage_WhenJsonErrorBodyExists()
	{
		var client = CreateClient(_ => CreateJsonResponse(
			"""
			{ "message": "Unsupported currency pair." }
			""",
			HttpStatusCode.BadRequest));

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLatestRateAsync("USD", "BTC"));

		Assert.Equal("Frankfurter error 400: Unsupported currency pair.", exception.Message);
	}

	[Fact]
	public async Task GetLatestRateAsync_UsesFallbackErrorMessage_WhenErrorBodyIsNotJson()
	{
		var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
		{
			Content = new StringContent("not-json", Encoding.UTF8, "text/plain")
		});

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLatestRateAsync("USD", "BTC"));

		Assert.Equal("Frankfurter could not find the requested currency pair.", exception.Message);
	}

	[Fact]
	public async Task GetLatestRateAsync_ThrowsFriendlyMessage_ForInvalidJson()
	{
		var client = CreateClient(_ => CreateJsonResponse("{ invalid json ]"));

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLatestRateAsync("USD", "EUR"));

		Assert.Equal("Frankfurter returned data in an unexpected format.", exception.Message);
	}

	[Fact]
	public async Task GetLatestRateAsync_ThrowsFriendlyMessage_ForInvalidDate()
	{
		var client = CreateClient(_ => CreateJsonResponse("""
			{ "base": "USD", "quote": "EUR", "date": "not-a-date", "rate": 0.9 }
			"""));

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetLatestRateAsync("USD", "EUR"));

		Assert.Equal("Frankfurter returned an invalid date: not-a-date", exception.Message);
	}

	private static FrankfurterApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
	{
		var httpClient = new HttpClient(new DelegateHttpMessageHandler(handler))
		{
			BaseAddress = new Uri("https://example.test/")
		};

		return new FrankfurterApiClient(httpClient);
	}

	private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};
	}

	private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(handler(request));
		}
	}
}
