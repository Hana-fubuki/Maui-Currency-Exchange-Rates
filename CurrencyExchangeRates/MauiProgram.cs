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
			.ConfigureMauiHandlers(handlers =>
			{
				Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FlatChrome", (handler, view) =>
				{
#if WINDOWS
					if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("FlatEntryTextBoxStyle", out var styleObject) &&
					    styleObject is Microsoft.UI.Xaml.Style style)
					{
						handler.PlatformView.Style = style;
					}

					handler.PlatformView.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
					handler.PlatformView.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
				});

				Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("FlatChrome", (handler, view) =>
				{
#if WINDOWS
					handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
					handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
					handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
					handler.PlatformView.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
					handler.PlatformView.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
				});
			})
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
