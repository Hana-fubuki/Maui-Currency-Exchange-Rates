using CurrencyExchangeRates.Features.Exchange;

namespace CurrencyExchangeRates;

public partial class AppShell : Shell
{
	public AppShell(ExchangePage exchangePage)
	{
		InitializeComponent();

		var exchangeContent = new ShellContent
		{
			Title = "Exchange",
			Route = nameof(ExchangePage),
			Content = exchangePage
		};

		Items.Add(exchangeContent);
		CurrentItem = exchangeContent;
	}
}
