namespace CurrencyExchangeRates.Features.Exchange;

public partial class ExchangePage : ContentPage
{
	private readonly ExchangeViewModel viewModel;
	private bool hasLoaded;

	public ExchangePage(ExchangeViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = this.viewModel = viewModel;
	}

	private async void OnLoaded(object? sender, EventArgs e)
	{
		if (hasLoaded)
		{
			return;
		}

		hasLoaded = true;
		await viewModel.InitializeAsync();
	}
}
