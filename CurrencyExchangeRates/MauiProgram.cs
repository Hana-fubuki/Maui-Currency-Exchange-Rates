using CurrencyExchangeRates.Features.Exchange;
using CurrencyExchangeRates.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CurrencyExchangeRates;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(new HttpClient
		{
			BaseAddress = new Uri("https://api.frankfurter.dev/v2/")
		});
		builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
		builder.Services.AddSingleton<IFrankfurterApiClient, FrankfurterApiClient>();
		builder.Services.AddSingleton<IExchangeRatesService, ExchangeRatesService>();
		builder.Services.AddSingleton<ExchangeViewModel>();
		builder.Services.AddSingleton<ExchangePage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
