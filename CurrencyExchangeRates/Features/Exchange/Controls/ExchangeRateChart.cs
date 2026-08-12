using System.Collections;
using CurrencyExchangeRates.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CurrencyExchangeRates.Features.Exchange.Controls;

public sealed class ExchangeRateChart : GraphicsView
{
	public static readonly BindableProperty PointsProperty = BindableProperty.Create(
		nameof(Points),
		typeof(IList<HistoricalRatePoint>),
		typeof(ExchangeRateChart),
		default(IList<HistoricalRatePoint>),
		propertyChanged: OnPointsChanged);

	public static readonly BindableProperty SelectedPointProperty = BindableProperty.Create(
		nameof(SelectedPoint),
		typeof(HistoricalRatePoint),
		typeof(ExchangeRateChart),
		default(HistoricalRatePoint),
		BindingMode.TwoWay,
		propertyChanged: OnSelectedPointChanged);

	private readonly ExchangeRateChartDrawable drawable = new();

	public ExchangeRateChart()
	{
		Drawable = drawable;

		StartHoverInteraction += OnPointerMoved;
		MoveHoverInteraction += OnPointerMoved;
		EndHoverInteraction += OnPointerExited;
		StartInteraction += OnPointerMoved;
		DragInteraction += OnPointerMoved;
		EndInteraction += OnInteractionEnded;
		CancelInteraction += OnPointerExited;
	}

	public IList<HistoricalRatePoint>? Points
	{
		get => (IList<HistoricalRatePoint>?)GetValue(PointsProperty);
		set => SetValue(PointsProperty, value);
	}

	public HistoricalRatePoint? SelectedPoint
	{
		get => (HistoricalRatePoint?)GetValue(SelectedPointProperty);
		set => SetValue(SelectedPointProperty, value);
	}

	private static void OnPointsChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.drawable.Points = ConvertPoints(newValue);

		if (chart.drawable.Points.Count > 0 && chart.SelectedPoint is null)
		{
			chart.SelectedPoint = chart.drawable.Points[^1];
		}

		chart.UpdateSelectionIndex();
		chart.Invalidate();
	}

	private static void OnSelectedPointChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.UpdateSelectionIndex();
		chart.Invalidate();
	}

	private static List<HistoricalRatePoint> ConvertPoints(object? points)
	{
		return points switch
		{
			IList<HistoricalRatePoint> list => [.. list],
			IEnumerable enumerable => enumerable.OfType<HistoricalRatePoint>().ToList(),
			_ => []
		};
	}

	private void OnPointerMoved(object? sender, TouchEventArgs e)
	{
		if (drawable.Points.Count == 0 || e.Touches.Length == 0)
		{
			return;
		}

		var point = drawable.FindNearestPoint(e.Touches[0]);
		if (point is not null)
		{
			SelectedPoint = point;
		}
	}

	private void OnInteractionEnded(object? sender, TouchEventArgs e)
	{
		if (drawable.Points.Count == 0)
		{
			return;
		}

		if (e.Touches.Length > 0)
		{
			var point = drawable.FindNearestPoint(e.Touches[0]);
			if (point is not null)
			{
				SelectedPoint = point;
				return;
			}
		}

		SelectedPoint = drawable.Points[^1];
	}

	private void OnPointerExited(object? sender, EventArgs e)
	{
		if (drawable.Points.Count == 0)
		{
			return;
		}

		SelectedPoint = drawable.Points[^1];
	}

	private void UpdateSelectionIndex()
	{
		drawable.SelectedIndex = SelectedPoint is null
			? -1
			: drawable.Points.FindIndex(point => point.Equals(SelectedPoint));
	}

	private sealed class ExchangeRateChartDrawable : IDrawable
	{
		private readonly Color axisColor = Color.FromArgb("#A8A8B3");
		private readonly Color fillColor = Color.FromArgb("#DDE6FF");
		private readonly Color guideColor = Color.FromArgb("#C9CDE0");
		private readonly Color lineColor = Color.FromArgb("#512BD4");
		private readonly Color selectedPointColor = Color.FromArgb("#512BD4");
		private readonly Color textColor = Color.FromArgb("#6E6E6E");
		private RectF plotRect;

		public List<HistoricalRatePoint> Points { get; set; } = [];

		public int SelectedIndex { get; set; } = -1;

		public void Draw(ICanvas canvas, RectF dirtyRect)
		{
			plotRect = new RectF(
				x: 18,
				y: 20,
				width: Math.Max(dirtyRect.Width - 36, 0),
				height: Math.Max(dirtyRect.Height - 58, 0));

			if (Points.Count == 0 || plotRect.Width <= 0 || plotRect.Height <= 0)
			{
				return;
			}

			canvas.Antialias = true;
			DrawGrid(canvas);
			DrawSeries(canvas);
			DrawSelection(canvas);
			DrawAxisLabels(canvas);
		}

		public HistoricalRatePoint? FindNearestPoint(PointF interactionPoint)
		{
			if (Points.Count == 0 || plotRect.Width <= 0)
			{
				return null;
			}

			var clampedX = Math.Clamp(interactionPoint.X, plotRect.Left, plotRect.Right);
			var ratio = (clampedX - plotRect.Left) / plotRect.Width;
			var index = (int)Math.Round(ratio * (Points.Count - 1), MidpointRounding.AwayFromZero);
			index = Math.Clamp(index, 0, Points.Count - 1);
			return Points[index];
		}

		private void DrawGrid(ICanvas canvas)
		{
			canvas.StrokeColor = guideColor;
			canvas.StrokeSize = 1;

			for (var row = 0; row < 3; row++)
			{
				var y = plotRect.Top + plotRect.Height / 2f * row;
				canvas.DrawLine(plotRect.Left, y, plotRect.Right, y);
			}

			canvas.StrokeColor = axisColor;
			canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
		}

		private void DrawSeries(ICanvas canvas)
		{
			var path = new PathF();
			var fillPath = new PathF();
			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);

			for (var index = 0; index < Points.Count; index++)
			{
				var coordinate = GetCoordinate(index, minRate, maxRate);
				if (index == 0)
				{
					path.MoveTo(coordinate);
					fillPath.MoveTo(coordinate.X, plotRect.Bottom);
					fillPath.LineTo(coordinate);
				}
				else
				{
					path.LineTo(coordinate);
					fillPath.LineTo(coordinate);
				}
			}

			fillPath.LineTo(plotRect.Right, plotRect.Bottom);
			fillPath.Close();

			canvas.FillColor = fillColor;
			canvas.FillPath(fillPath);

			canvas.StrokeColor = lineColor;
			canvas.StrokeSize = 3;
			canvas.DrawPath(path);
		}

		private void DrawSelection(ICanvas canvas)
		{
			if (SelectedIndex < 0 || SelectedIndex >= Points.Count)
			{
				return;
			}

			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);
			var coordinate = GetCoordinate(SelectedIndex, minRate, maxRate);

			canvas.StrokeColor = guideColor;
			canvas.StrokeSize = 1.5f;
			canvas.DrawLine(coordinate.X, plotRect.Top, coordinate.X, plotRect.Bottom);

			canvas.FillColor = Colors.White;
			canvas.FillCircle(coordinate.X, coordinate.Y, 8);

			canvas.StrokeColor = selectedPointColor;
			canvas.StrokeSize = 3;
			canvas.DrawCircle(coordinate.X, coordinate.Y, 8);
		}

		private void DrawAxisLabels(ICanvas canvas)
		{
			canvas.FontColor = textColor;
			canvas.FontSize = 12;

			var first = Points[0];
			var last = Points[^1];
			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);

			canvas.DrawString(
				FormatDate(first.Date),
				plotRect.Left,
				plotRect.Bottom + 10,
				90,
				20,
				HorizontalAlignment.Left,
				VerticalAlignment.Top);

			canvas.DrawString(
				FormatDate(last.Date),
				plotRect.Right - 90,
				plotRect.Bottom + 10,
				90,
				20,
				HorizontalAlignment.Right,
				VerticalAlignment.Top);

			canvas.DrawString(
				$"{maxRate:N4}",
				plotRect.Left,
				plotRect.Top - 18,
				100,
				18,
				HorizontalAlignment.Left,
				VerticalAlignment.Top);

			canvas.DrawString(
				$"{minRate:N4}",
				plotRect.Left,
				plotRect.Bottom - 18,
				100,
				18,
				HorizontalAlignment.Left,
				VerticalAlignment.Bottom);
		}

		private PointF GetCoordinate(int index, decimal minRate, decimal maxRate)
		{
			var x = Points.Count == 1
				? plotRect.Left
				: plotRect.Left + plotRect.Width * index / (Points.Count - 1);

			var yRatio = maxRate == minRate
				? 0.5f
				: (float)((Points[index].Rate - minRate) / (maxRate - minRate));

			var y = plotRect.Bottom - plotRect.Height * yRatio;
			return new PointF(x, y);
		}

		private static string FormatDate(DateOnly date)
		{
			return date.Day == 1
				? date.ToString("MMM yyyy")
				: date.ToString("MMM d");
		}
	}
}
