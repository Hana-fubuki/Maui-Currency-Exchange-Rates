using Microsoft.Maui.ApplicationModel;

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

	private async void OnFrankfurterTapped(object? sender, TappedEventArgs e)
	{
		await Launcher.Default.OpenAsync(new Uri("https://frankfurter.dev/"));
	}

	private void OnInputAreaFocused(object? sender, FocusEventArgs e)
	{
		UpdateInputChromeStates();
	}

	private void OnInputAreaUnfocused(object? sender, FocusEventArgs e)
	{
		Dispatcher.Dispatch(UpdateInputChromeStates);
	}

	private void UpdateInputChromeStates()
	{
		SetInputChromeState(
			BaseInputContainer,
			BaseAmountEntry.IsFocused || BaseCurrencyPicker.IsFocused);
		SetInputChromeState(
			QuoteInputContainer,
			QuoteAmountEntry.IsFocused || QuoteCurrencyPicker.IsFocused);
	}

	private static void SetInputChromeState(Border container, bool isFocused)
	{
		if (isFocused)
		{
			container.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
				? Color.FromArgb("#8AB4F8")
				: Color.FromArgb("#A8C7FA");
			return;
		}

		container.Stroke = Color.FromArgb("#5F6368");
	}
}
