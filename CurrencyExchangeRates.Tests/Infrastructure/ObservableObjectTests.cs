using System.ComponentModel;
using CurrencyExchangeRates.Infrastructure;

namespace CurrencyExchangeRates.Tests.Infrastructure;

public sealed class ObservableObjectTests
{
	[Fact]
	public void SetValue_RaisesPropertyChanged_WhenValueChanges()
	{
		var subject = new TestObservableObject();
		var notifications = new List<string?>();
		subject.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

		subject.Name = "USD";

		Assert.Equal("USD", subject.Name);
		Assert.Equal(["Name"], notifications);
	}

	[Fact]
	public void SetValue_DoesNotRaisePropertyChanged_WhenValueIsSame()
	{
		var subject = new TestObservableObject();
		var notifications = new List<string?>();
		subject.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

		subject.Name = string.Empty;

		Assert.Empty(notifications);
	}

	private sealed class TestObservableObject : ObservableObject
	{
		private string name = string.Empty;

		public string Name
		{
			get => name;
			set => SetProperty(ref name, value);
		}
	}
}
