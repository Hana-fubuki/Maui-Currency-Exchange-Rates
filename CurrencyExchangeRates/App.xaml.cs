using Microsoft.Extensions.DependencyInjection;

namespace CurrencyExchangeRates;

public partial class App : Application
{
	private readonly AppShell appShell;

	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		appShell = serviceProvider.GetRequiredService<AppShell>();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(appShell);
	}
}