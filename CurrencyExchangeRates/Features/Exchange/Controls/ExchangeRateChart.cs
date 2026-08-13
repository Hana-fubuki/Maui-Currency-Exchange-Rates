using System.Collections;
using System.Collections.Specialized;
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

	public static readonly BindableProperty SelectedRangeStartProperty = BindableProperty.Create(
		nameof(SelectedRangeStart),
		typeof(HistoricalRatePoint),
		typeof(ExchangeRateChart),
		default(HistoricalRatePoint),
		BindingMode.TwoWay,
		propertyChanged: OnRangeSelectionChanged);

	public static readonly BindableProperty SelectedRangeEndProperty = BindableProperty.Create(
		nameof(SelectedRangeEnd),
		typeof(HistoricalRatePoint),
		typeof(ExchangeRateChart),
		default(HistoricalRatePoint),
		BindingMode.TwoWay,
		propertyChanged: OnRangeSelectionChanged);

	public static readonly BindableProperty LineColorProperty = BindableProperty.Create(
		nameof(LineColor),
		typeof(Color),
		typeof(ExchangeRateChart),
		Color.FromArgb("#188038"),
		propertyChanged: OnVisualPropertyChanged);

	public static readonly BindableProperty FillTopColorProperty = BindableProperty.Create(
		nameof(FillTopColor),
		typeof(Color),
		typeof(ExchangeRateChart),
		Color.FromArgb("#33188038"),
		propertyChanged: OnVisualPropertyChanged);

	public static readonly BindableProperty FillBottomColorProperty = BindableProperty.Create(
		nameof(FillBottomColor),
		typeof(Color),
		typeof(ExchangeRateChart),
		Color.FromArgb("#05188038"),
		propertyChanged: OnVisualPropertyChanged);

	private readonly ExchangeRateChartDrawable drawable = new();
	private int dragAnchorIndex = -1;
	private bool isDraggingRange;
	private INotifyCollectionChanged? observedPointsCollection;
	private bool showTooltip;

	public ExchangeRateChart()
	{
		Drawable = drawable;

		StartHoverInteraction += OnPointerMoved;
		MoveHoverInteraction += OnPointerMoved;
		EndHoverInteraction += OnPointerExited;
		StartInteraction += OnInteractionStarted;
		DragInteraction += OnRangeDragged;
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

	public HistoricalRatePoint? SelectedRangeStart
	{
		get => (HistoricalRatePoint?)GetValue(SelectedRangeStartProperty);
		set => SetValue(SelectedRangeStartProperty, value);
	}

	public HistoricalRatePoint? SelectedRangeEnd
	{
		get => (HistoricalRatePoint?)GetValue(SelectedRangeEndProperty);
		set => SetValue(SelectedRangeEndProperty, value);
	}

	public Color LineColor
	{
		get => (Color)GetValue(LineColorProperty);
		set => SetValue(LineColorProperty, value);
	}

	public Color FillTopColor
	{
		get => (Color)GetValue(FillTopColorProperty);
		set => SetValue(FillTopColorProperty, value);
	}

	public Color FillBottomColor
	{
		get => (Color)GetValue(FillBottomColorProperty);
		set => SetValue(FillBottomColorProperty, value);
	}

	private static void OnPointsChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.DetachPointsCollection(oldValue);
		chart.AttachPointsCollection(newValue);
		chart.RefreshPoints(newValue);
	}

	private static void OnSelectedPointChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.UpdateSelectionIndex();
		chart.Invalidate();
	}

	private static void OnRangeSelectionChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.UpdateRangeSelection();
		chart.Invalidate();
	}

	private static void OnVisualPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		var chart = (ExchangeRateChart)bindable;
		chart.drawable.LineColor = chart.LineColor;
		chart.drawable.FillTopColor = chart.FillTopColor;
		chart.drawable.FillBottomColor = chart.FillBottomColor;
		chart.drawable.SelectedPointColor = chart.LineColor;
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

	private void AttachPointsCollection(object? points)
	{
		observedPointsCollection = points as INotifyCollectionChanged;
		if (observedPointsCollection is not null)
		{
			observedPointsCollection.CollectionChanged += OnPointsCollectionChanged;
		}
	}

	private void DetachPointsCollection(object? points)
	{
		var collection = points as INotifyCollectionChanged ?? observedPointsCollection;
		if (collection is not null)
		{
			collection.CollectionChanged -= OnPointsCollectionChanged;
		}

		if (ReferenceEquals(collection, observedPointsCollection))
		{
			observedPointsCollection = null;
		}
	}

	private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshPoints(sender);
	}

	private void RefreshPoints(object? points)
	{
		drawable.Points = ConvertPoints(points);

		if (drawable.Points.Count == 0)
		{
			SelectedPoint = null;
			showTooltip = false;
			ClearRangeSelection();
		}
		else if (SelectedPoint is null || !drawable.Points.Contains(SelectedPoint))
		{
			SelectedPoint = drawable.Points[^1];
		}

		if ((SelectedRangeStart is not null && !drawable.Points.Contains(SelectedRangeStart)) ||
			(SelectedRangeEnd is not null && !drawable.Points.Contains(SelectedRangeEnd)))
		{
			ClearRangeSelection();
		}

		UpdateSelectionIndex();
		UpdateRangeSelection();
		Invalidate();
	}

	private void OnPointerMoved(object? sender, TouchEventArgs e)
	{
		if (drawable.Points.Count == 0 || e.Touches.Length == 0 || isDraggingRange)
		{
			return;
		}

		var point = drawable.FindNearestPoint(e.Touches[0]);
		if (point is not null)
		{
			showTooltip = true;
			SelectedPoint = point;
		}
	}

	private void OnInteractionStarted(object? sender, TouchEventArgs e)
	{
		if (drawable.Points.Count == 0 || e.Touches.Length == 0)
		{
			return;
		}

		if (HasSelectedRange())
		{
			ClearRangeSelection();
			return;
		}

		var point = drawable.FindNearestPoint(e.Touches[0]);
		if (point is null)
		{
			return;
		}

		dragAnchorIndex = drawable.Points.FindIndex(candidate => candidate.Equals(point));
		if (dragAnchorIndex < 0)
		{
			return;
		}

		isDraggingRange = true;
		showTooltip = true;
		SelectedPoint = point;
		SelectedRangeStart = point;
		SelectedRangeEnd = point;
	}

	private void OnRangeDragged(object? sender, TouchEventArgs e)
	{
		if (!isDraggingRange || drawable.Points.Count == 0 || e.Touches.Length == 0)
		{
			return;
		}

		var point = drawable.FindNearestPoint(e.Touches[0]);
		if (point is null)
		{
			return;
		}

		showTooltip = true;
		SelectedPoint = point;
		SelectedRangeEnd = point;
	}

	private void OnInteractionEnded(object? sender, TouchEventArgs e)
	{
		if (drawable.Points.Count == 0)
		{
			return;
		}

		if (isDraggingRange)
		{
			isDraggingRange = false;

			if (e.Touches.Length > 0)
			{
				var point = drawable.FindNearestPoint(e.Touches[0]);
				if (point is not null)
				{
					SelectedPoint = point;
					SelectedRangeEnd = point;
				}
			}

			if (!HasSelectedRange())
			{
				ClearRangeSelection();
			}

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

		if (!HasSelectedRange())
		{
			showTooltip = false;
			SelectedPoint = drawable.Points[^1];
		}

		drawable.ShowTooltip = showTooltip;
		Invalidate();
	}

	private void UpdateSelectionIndex()
	{
		drawable.SelectedIndex = SelectedPoint is null
			? -1
			: drawable.Points.FindIndex(point => point.Equals(SelectedPoint));
		drawable.ShowTooltip = showTooltip;
	}

	private void UpdateRangeSelection()
	{
		drawable.RangeStartIndex = SelectedRangeStart is null
			? -1
			: drawable.Points.FindIndex(point => point.Equals(SelectedRangeStart));
		drawable.RangeEndIndex = SelectedRangeEnd is null
			? -1
			: drawable.Points.FindIndex(point => point.Equals(SelectedRangeEnd));
	}

	private bool HasSelectedRange()
	{
		return SelectedRangeStart is not null &&
			SelectedRangeEnd is not null &&
			!SelectedRangeStart.Equals(SelectedRangeEnd);
	}

	private void ClearRangeSelection()
	{
		isDraggingRange = false;
		dragAnchorIndex = -1;
		SelectedRangeStart = null;
		SelectedRangeEnd = null;
	}

	private sealed class ExchangeRateChartDrawable : IDrawable
	{
		private readonly Color axisColor = Color.FromArgb("#A8A8B3");
		private readonly Color guideColor = Color.FromArgb("#C9CDE0");
		private readonly Color textColor = Color.FromArgb("#6E6E6E");
		private readonly Color negativeTrendColor = Color.FromArgb("#D93025");
		private readonly Color positiveTrendColor = Color.FromArgb("#188038");
		private readonly Color tooltipBackgroundColor = Color.FromArgb("#D9202124");
		private readonly Color tooltipBorderColor = Color.FromArgb("#663C4043");
		private readonly Color tooltipTextColor = Colors.White;
		private RectF plotRect;

		public Color FillBottomColor { get; set; } = Color.FromArgb("#05188038");

		public Color FillTopColor { get; set; } = Color.FromArgb("#33188038");

		public Color LineColor { get; set; } = Color.FromArgb("#188038");

		public List<HistoricalRatePoint> Points { get; set; } = [];

		public int RangeEndIndex { get; set; } = -1;

		public int RangeStartIndex { get; set; } = -1;

		public Color SelectedPointColor { get; set; } = Color.FromArgb("#188038");

		public int SelectedIndex { get; set; } = -1;

		public bool ShowTooltip { get; set; }

		public void Draw(ICanvas canvas, RectF dirtyRect)
		{
			plotRect = new RectF(
				x: 64,
				y: 18,
				width: Math.Max(dirtyRect.Width - 82, 0),
				height: Math.Max(dirtyRect.Height - 52, 0));

			if (Points.Count == 0 || plotRect.Width <= 0 || plotRect.Height <= 0)
			{
				return;
			}

			canvas.Antialias = true;
			DrawGrid(canvas);
			DrawSeries(canvas);
			DrawSelection(canvas);
			DrawAxisLabels(canvas);
			DrawTooltip(canvas);
		}

		public HistoricalRatePoint? FindNearestPoint(PointF interactionPoint)
		{
			if (Points.Count == 0 || plotRect.Width <= 0)
			{
				return null;
			}

			var clampedX = Math.Clamp(interactionPoint.X, plotRect.Left, plotRect.Right);
			var firstDate = Points[0].Date;
			var lastDate = Points[^1].Date;

			return Points
				.OrderBy(point => Math.Abs(GetXCoordinate(point.Date, firstDate, lastDate) - clampedX))
				.FirstOrDefault();
		}

		private void DrawGrid(ICanvas canvas)
		{
			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);
			var yTicks = CreateYTicks(minRate, maxRate);

			canvas.StrokeColor = guideColor;
			canvas.StrokeSize = 1;

			foreach (var tick in yTicks)
			{
				var y = GetYCoordinate(tick, minRate, maxRate, yTicks);
				canvas.DrawLine(plotRect.Left, y, plotRect.Right, y);
			}

			canvas.StrokeColor = axisColor;
			canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
		}

		private void DrawSeries(ICanvas canvas)
		{
			var path = new PathF();
			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);
			var yTicks = CreateYTicks(minRate, maxRate);
			var firstDate = Points[0].Date;
			var lastDate = Points[^1].Date;

			for (var index = 0; index < Points.Count; index++)
			{
				var coordinate = GetCoordinate(Points[index], firstDate, lastDate, minRate, maxRate, yTicks);
				if (index == 0)
				{
					path.MoveTo(coordinate);
				}
				else
				{
					path.LineTo(coordinate);
				}
			}

			var fillPath = HasSelectedRange()
				? CreateSelectedFillPath(firstDate, lastDate, minRate, maxRate, yTicks)
				: CreateFullFillPath(firstDate, lastDate, minRate, maxRate, yTicks);

			if (fillPath is not null)
			{
				canvas.SetFillPaint(
					new LinearGradientPaint
					{
						StartColor = FillTopColor,
						EndColor = FillBottomColor,
						StartPoint = new Point(0, 0),
						EndPoint = new Point(0, 1)
					},
					plotRect);
				canvas.FillPath(fillPath);
			}

			canvas.StrokeColor = LineColor;
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
			var yTicks = CreateYTicks(minRate, maxRate);
			var coordinate = GetCoordinate(Points[SelectedIndex], Points[0].Date, Points[^1].Date, minRate, maxRate, yTicks);

			if (HasSelectedRange())
			{
				var ordered = GetOrderedRange();
				var startCoordinate = GetCoordinate(Points[ordered.Start], Points[0].Date, Points[^1].Date, minRate, maxRate, yTicks);
				var endCoordinate = GetCoordinate(Points[ordered.End], Points[0].Date, Points[^1].Date, minRate, maxRate, yTicks);

				canvas.StrokeColor = guideColor;
				canvas.StrokeSize = 1.5f;
				canvas.DrawLine(startCoordinate.X, plotRect.Top, startCoordinate.X, plotRect.Bottom);
				canvas.DrawLine(endCoordinate.X, plotRect.Top, endCoordinate.X, plotRect.Bottom);
			}
			else
			{
				canvas.StrokeColor = guideColor;
				canvas.StrokeSize = 1.5f;
				canvas.DrawLine(coordinate.X, plotRect.Top, coordinate.X, plotRect.Bottom);
			}

			canvas.FillColor = Colors.White;
			canvas.FillCircle(coordinate.X, coordinate.Y, 8);

			canvas.StrokeColor = SelectedPointColor;
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

			var yTicks = CreateYTicks(minRate, maxRate);
			var yStep = yTicks.Count > 1 ? yTicks[1] - yTicks[0] : 0m;
			foreach (var tick in yTicks)
			{
				var y = GetYCoordinate(tick, minRate, maxRate, yTicks);
				canvas.DrawString(
					FormatRateLabel(tick, yStep),
					0,
					y - 10,
					plotRect.Left - 10,
					20,
					HorizontalAlignment.Right,
					VerticalAlignment.Center);
			}

			foreach (var tickDate in CreateXTickDates(first.Date, last.Date))
			{
				var x = GetXCoordinate(tickDate, first.Date, last.Date);
				canvas.DrawString(
					FormatDateLabel(tickDate, first.Date, last.Date),
					x - 45,
					plotRect.Bottom + 10,
					90,
					20,
					HorizontalAlignment.Center,
					VerticalAlignment.Top);
			}
		}

		private void DrawTooltip(ICanvas canvas)
		{
			if (!ShowTooltip || SelectedIndex < 0 || SelectedIndex >= Points.Count)
			{
				return;
			}

			var minRate = Points.Min(point => point.Rate);
			var maxRate = Points.Max(point => point.Rate);
			var yTicks = CreateYTicks(minRate, maxRate);
			var point = Points[SelectedIndex];
			var coordinate = GetCoordinate(point, Points[0].Date, Points[^1].Date, minRate, maxRate, yTicks);
			var hasRange = HasSelectedRange();
			const float tooltipWidth = 184;
			var tooltipHeight = hasRange ? 64f : 52f;
			var tooltipX = Math.Clamp(coordinate.X + 12, plotRect.Left, plotRect.Right - tooltipWidth);
			var tooltipY = Math.Clamp(coordinate.Y - tooltipHeight - 12, plotRect.Top, plotRect.Bottom - tooltipHeight);

			canvas.FillColor = tooltipBackgroundColor;
			canvas.FillRoundedRectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 12);
			canvas.StrokeColor = tooltipBorderColor;
			canvas.StrokeSize = 1;
			canvas.DrawRoundedRectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 12);

			if (hasRange)
			{
				var ordered = GetOrderedRange();
				var startPoint = Points[ordered.Start];
				var endPoint = Points[ordered.End];
				var delta = endPoint.Rate - startPoint.Rate;
				var percent = startPoint.Rate == 0m ? 0m : delta / startPoint.Rate * 100m;
				var sign = delta >= 0m ? "+" : string.Empty;
				var trendColor = delta switch
				{
					> 0m => positiveTrendColor,
					< 0m => negativeTrendColor,
					_ => tooltipTextColor
				};

				canvas.FontColor = tooltipTextColor;
				canvas.FontSize = 11;
				canvas.DrawString(
					$"{startPoint.Date:MMM d, yyyy} - {endPoint.Date:MMM d, yyyy}",
					tooltipX + 12,
					tooltipY + 8,
					tooltipWidth - 24,
					16,
					HorizontalAlignment.Left,
					VerticalAlignment.Top);

				canvas.FontColor = trendColor;
				canvas.FontSize = 14;
				canvas.DrawString(
					$"{sign}{delta:N4} ({sign}{percent:N2}%)",
					tooltipX + 12,
					tooltipY + 28,
					tooltipWidth - 24,
					18,
					HorizontalAlignment.Left,
					VerticalAlignment.Top);
			}
			else
			{
				canvas.FontColor = tooltipTextColor;
				canvas.FontSize = 11;
				canvas.DrawString(
					point.Date.ToString("MMM d, yyyy"),
					tooltipX + 12,
					tooltipY + 8,
					tooltipWidth - 24,
					16,
					HorizontalAlignment.Left,
					VerticalAlignment.Top);

				canvas.FontColor = LineColor;
				canvas.FontSize = 14;
				canvas.DrawString(
					FormatTooltipRate(point.Rate),
					tooltipX + 12,
					tooltipY + 24,
					tooltipWidth - 24,
					18,
					HorizontalAlignment.Left,
					VerticalAlignment.Top);
			}
		}

		private PointF GetCoordinate(
			HistoricalRatePoint point,
			DateOnly firstDate,
			DateOnly lastDate,
			decimal minRate,
			decimal maxRate,
			IReadOnlyList<decimal> yTicks)
		{
			var x = GetXCoordinate(point.Date, firstDate, lastDate);
			var y = GetYCoordinate(point.Rate, minRate, maxRate, yTicks);
			return new PointF(x, y);
		}

		private bool HasSelectedRange()
		{
			return RangeStartIndex >= 0 && RangeEndIndex >= 0 && RangeStartIndex != RangeEndIndex;
		}

		private (int Start, int End) GetOrderedRange()
		{
			return RangeStartIndex <= RangeEndIndex
				? (RangeStartIndex, RangeEndIndex)
				: (RangeEndIndex, RangeStartIndex);
		}

		private PathF CreateFullFillPath(
			DateOnly firstDate,
			DateOnly lastDate,
			decimal minRate,
			decimal maxRate,
			IReadOnlyList<decimal> yTicks)
		{
			var fillPath = new PathF();

			for (var index = 0; index < Points.Count; index++)
			{
				var coordinate = GetCoordinate(Points[index], firstDate, lastDate, minRate, maxRate, yTicks);
				if (index == 0)
				{
					fillPath.MoveTo(coordinate.X, plotRect.Bottom);
					fillPath.LineTo(coordinate);
				}
				else
				{
					fillPath.LineTo(coordinate);
				}
			}

			fillPath.LineTo(plotRect.Right, plotRect.Bottom);
			fillPath.Close();
			return fillPath;
		}

		private PathF? CreateSelectedFillPath(
			DateOnly firstDate,
			DateOnly lastDate,
			decimal minRate,
			decimal maxRate,
			IReadOnlyList<decimal> yTicks)
		{
			var ordered = GetOrderedRange();
			if (ordered.Start < 0 || ordered.End < 0 || ordered.End >= Points.Count)
			{
				return null;
			}

			var fillPath = new PathF();
			var startCoordinate = GetCoordinate(Points[ordered.Start], firstDate, lastDate, minRate, maxRate, yTicks);
			fillPath.MoveTo(startCoordinate.X, plotRect.Bottom);
			fillPath.LineTo(startCoordinate);

			for (var index = ordered.Start + 1; index <= ordered.End; index++)
			{
				fillPath.LineTo(GetCoordinate(Points[index], firstDate, lastDate, minRate, maxRate, yTicks));
			}

			var endCoordinate = GetCoordinate(Points[ordered.End], firstDate, lastDate, minRate, maxRate, yTicks);
			fillPath.LineTo(endCoordinate.X, plotRect.Bottom);
			fillPath.Close();
			return fillPath;
		}

		private float GetXCoordinate(DateOnly date, DateOnly firstDate, DateOnly lastDate)
		{
			var totalDays = Math.Max(lastDate.DayNumber - firstDate.DayNumber, 1);
			var elapsedDays = date.DayNumber - firstDate.DayNumber;
			return plotRect.Left + plotRect.Width * elapsedDays / totalDays;
		}

		private float GetYCoordinate(decimal value, decimal minRate, decimal maxRate, IReadOnlyList<decimal> yTicks)
		{
			var chartMin = yTicks.Count > 0 ? yTicks[0] : minRate;
			var chartMax = yTicks.Count > 0 ? yTicks[^1] : maxRate;
			var yRatio = chartMax == chartMin
				? 0.5f
				: (float)((value - chartMin) / (chartMax - chartMin));

			return plotRect.Bottom - plotRect.Height * yRatio;
		}

		private static List<decimal> CreateYTicks(decimal minRate, decimal maxRate)
		{
			if (minRate == maxRate)
			{
				minRate -= 1m;
				maxRate += 1m;
			}

			const int desiredTickCount = 5;
			var range = maxRate - minRate;
			var step = GetNiceStep(range / (desiredTickCount - 1));
			var niceMin = Math.Floor(minRate / step) * step;
			var niceMax = Math.Ceiling(maxRate / step) * step;
			var ticks = new List<decimal>();

			for (var tick = niceMin; tick <= niceMax + step / 2; tick += step)
			{
				ticks.Add(decimal.Round(tick, 6));
			}

			return ticks;
		}

		private static decimal GetNiceStep(decimal roughStep)
		{
			if (roughStep <= 0)
			{
				return 1m;
			}

			var exponent = (int)Math.Floor(Math.Log10((double)roughStep));
			var magnitude = (decimal)Math.Pow(10, exponent);
			var normalized = roughStep / magnitude;
			var niceNormalized = normalized switch
			{
				<= 1m => 1m,
				<= 2m => 2m,
				<= 2.5m => 2.5m,
				<= 5m => 5m,
				_ => 10m
			};

			return niceNormalized * magnitude;
		}

		private static IReadOnlyList<DateOnly> CreateXTickDates(DateOnly firstDate, DateOnly lastDate)
		{
			var totalDays = lastDate.DayNumber - firstDate.DayNumber;

			if (totalDays <= 45)
			{
				return
				[
					firstDate.AddDays((int)Math.Round(totalDays / 3d)),
					firstDate.AddDays((int)Math.Round(totalDays * 2d / 3d))
				];
			}

			if (totalDays <= 730)
			{
				return
				[
					SnapToMonthStart(firstDate, totalDays, 0.4),
					SnapToMonthStart(firstDate, totalDays, 0.8)
				];
			}

			return
			[
				SnapToYearStart(firstDate, totalDays, 0.25),
				SnapToYearStart(firstDate, totalDays, 0.5),
				SnapToYearStart(firstDate, totalDays, 0.75)
			];
		}

		private static DateOnly SnapToMonthStart(DateOnly startDate, int totalDays, double ratio)
		{
			var target = startDate.AddDays((int)Math.Round(totalDays * ratio));
			return new DateOnly(target.Year, target.Month, 1);
		}

		private static DateOnly SnapToYearStart(DateOnly startDate, int totalDays, double ratio)
		{
			var target = startDate.AddDays((int)Math.Round(totalDays * ratio));
			return new DateOnly(target.Year, 1, 1);
		}

		private static string FormatDateLabel(DateOnly date, DateOnly firstDate, DateOnly lastDate)
		{
			return lastDate.DayNumber - firstDate.DayNumber <= 45
				? date.ToString("MMM d")
				: date.ToString("MMM yyyy");
		}

		private static string FormatRateLabel(decimal value, decimal step)
		{
			if (step >= 1m)
			{
				return value.ToString("N0");
			}

			if (step >= 0.1m)
			{
				return value.ToString("N1");
			}

			if (step >= 0.01m)
			{
				return value.ToString("N2");
			}

			return value.ToString("N4");
		}

		private static string FormatTooltipRate(decimal value)
		{
			return value >= 100m ? value.ToString("N2") : value.ToString("N4");
		}
	}
}
