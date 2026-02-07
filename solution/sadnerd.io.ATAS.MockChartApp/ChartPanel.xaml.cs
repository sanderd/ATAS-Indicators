using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using System.Drawing;
using Point = System.Windows.Point;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Reusable chart panel component with timeframe support
/// </summary>
public partial class ChartPanel : UserControl
{
    #region Constants

    private const int LeftMargin = 10;
    private const int RightMargin = 80;
    private const int TopMargin = 10;
    private const int BottomMargin = 30;
    
    // ============================================================
    // DEBUG: Volume histogram for PVSRA debugging - REMOVE LATER
    // ============================================================
    private const bool SHOW_DEBUG_VOLUME = true;
    private const int DEBUG_VOLUME_HEIGHT = 60;
    // ============================================================

    #endregion

    #region Fields

    private List<IndicatorCandle> _candles = new();
    private readonly List<Indicator> _activeIndicators = new();
    private ChartInfo _chartInfo;
    private Container _container;
    private Timeframe _timeframe = Timeframe.H1;

    private int _firstVisibleBar = 0;
    private int _barWidth = 8;
    private int _barSpacing = 2;
    private double _yScale = 1.0;
    private decimal _priceCenter;

    // Panning state
    private Point _panStart;
    private int _panStartBar;
    private decimal _panStartPriceCenter;

    // Axis dragging state
    private enum DragMode { None, ChartPan, XAxisScale, YAxisScale }
    private DragMode _dragMode = DragMode.None;
    private double _dragStartValue;

    #endregion

    #region Properties

    public Timeframe Timeframe
    {
        get => _timeframe;
        set
        {
            if (_timeframe != value)
            {
                _timeframe = value;
                LoadData();
                Canvas.InvalidateVisual();
            }
        }
    }

    public string Symbol { get; set; } = "ES";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            SelectionBorder.BorderBrush = value 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 180, 255)) 
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    /// <summary>
    /// Event raised when the remove button is clicked
    /// </summary>
    public event EventHandler? OnRemoveRequested;

    /// <summary>
    /// Event raised when the chart is clicked (for selection)
    /// </summary>
    public event EventHandler? OnChartClicked;

    private int _daysToLoad = 5;
    public int DaysToLoad
    {
        get => _daysToLoad;
        set
        {
            if (_daysToLoad != value && value > 0)
            {
                _daysToLoad = value;
                LoadData();
            }
        }
    }

    /// <summary>
    /// Get list of active indicators for settings UI
    /// </summary>
    public IReadOnlyList<Indicator> ActiveIndicators => _activeIndicators.AsReadOnly();

    #endregion

    #region Constructor

    public ChartPanel()
    {
        InitializeComponent();

        // Initialize container
        _container = new Container();
        _chartInfo = new ChartInfo(new List<IndicatorCandle>())
        {
            BarWidth = _barWidth,
            BarSpacing = _barSpacing,
            ChartOffsetX = LeftMargin,
            ChartOffsetY = TopMargin
        };

        // Populate timeframe combo
        foreach (var tf in TimeframeExtensions.CommonTimeframes)
        {
            TimeframeCombo.Items.Add(tf.ToDisplayString());
        }
        TimeframeCombo.SelectedIndex = 2; // Default to H1

        Loaded += ChartPanel_Loaded;
        SizeChanged += (_, _) => Canvas.InvalidateVisual();
    }

    #endregion

    #region Public Methods

    public void LoadData()
    {
        // Calculate bar count from days to load and timeframe
        int barsPerDay = _timeframe switch
        {
            Timeframe.M1 => 24 * 60,
            Timeframe.M5 => 24 * 12,
            Timeframe.M15 => 24 * 4,
            Timeframe.M30 => 24 * 2,
            Timeframe.H1 => 24,
            Timeframe.H4 => 6,
            Timeframe.Daily => 1,
            _ => 24
        };
        int barCount = Math.Max(100, _daysToLoad * barsPerDay);
        
        _candles = TimeframeAggregator.GetRecentBars(_timeframe, barCount);
        _chartInfo = new ChartInfo(_candles)
        {
            BarWidth = _barWidth,
            BarSpacing = _barSpacing,
            ChartOffsetX = LeftMargin,
            ChartOffsetY = TopMargin
        };

        // Reconfigure indicators with new data
        foreach (var indicator in _activeIndicators)
        {
            indicator.ChartInfo = _chartInfo;
            indicator.SetCandles(_candles);
            indicator.Calculate();
        }

        // Set initial view
        if (_candles.Count > 0)
        {
            _firstVisibleBar = Math.Max(0, _candles.Count - 100);
            var lastCandle = _candles[^1];
            _priceCenter = (lastCandle.High + lastCandle.Low) / 2;
        }
        
        // Force re-render after data loads
        Canvas.InvalidateVisual();
    }

    public void AddIndicator(Indicator indicator)
    {
        indicator.ChartInfo = _chartInfo;
        indicator.Container = _container;
        indicator.InstrumentInfo = new InstrumentInfo { TimeZone = 0, TickSize = 0.25m };
        indicator.SetCandles(_candles);
        indicator.Calculate();
        _activeIndicators.Add(indicator);
        Canvas.InvalidateVisual();
    }

    public void RemoveIndicator(Indicator indicator)
    {
        _activeIndicators.Remove(indicator);
        Canvas.InvalidateVisual();
    }

    public void Refresh()
    {
        Canvas.InvalidateVisual();
    }

    /// <summary>
    /// Get list of active indicator names for serialization
    /// </summary>
    public List<string> GetActiveIndicatorNames()
    {
        var names = new List<string>();
        foreach (var indicator in _activeIndicators)
        {
            var typeName = indicator.GetType().Name;
            names.Add(typeName);
        }
        return names;
    }

    /// <summary>
    /// Remove indicator by type name
    /// </summary>
    public void RemoveIndicatorByName(string typeName)
    {
        var toRemove = _activeIndicators.FirstOrDefault(i => i.GetType().Name == typeName);
        if (toRemove != null)
        {
            _activeIndicators.Remove(toRemove);
            Canvas.InvalidateVisual();
        }
    }

    /// <summary>
    /// Clear all indicators
    /// </summary>
    public void ClearIndicators()
    {
        _activeIndicators.Clear();
        Canvas.InvalidateVisual();
    }

    #endregion

    #region Event Handlers

    private void ChartPanel_Loaded(object sender, RoutedEventArgs e)
    {
        SymbolLabel.Text = Symbol;
        
        // Sync timeframe dropdown to actual timeframe
        var tfIndex = Array.IndexOf(TimeframeExtensions.CommonTimeframes, _timeframe);
        if (tfIndex >= 0)
        {
            TimeframeCombo.SelectedIndex = tfIndex;
        }
        
        LoadData();
    }

    private void TimeframeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeframeCombo.SelectedIndex >= 0 && TimeframeCombo.SelectedIndex < TimeframeExtensions.CommonTimeframes.Length)
        {
            Timeframe = TimeframeExtensions.CommonTimeframes[TimeframeCombo.SelectedIndex];
        }
    }

    private void ChartPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Raise chart clicked event for parent to handle selection
        OnChartClicked?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        // Raise remove requested event for parent to handle removal
        OnRemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Canvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Clear background
        canvas.Clear(new SKColor(26, 26, 46)); // #1a1a2e

        if (_candles.Count == 0)
        {
            // Draw loading message
            using var paint = new SKPaint { Color = SKColors.White, TextSize = 14 };
            canvas.DrawText("Loading...", 50, 50, paint);
            return;
        }

        // Skip render if dimensions are invalid (happens before layout pass)
        if (info.Width < 100 || info.Height < 100)
        {
            return;
        }

        // Update dimensions
        int chartWidth = info.Width - LeftMargin - RightMargin;
        int chartHeight = info.Height - TopMargin - BottomMargin;

        _container.Region = new Rectangle(LeftMargin, TopMargin, chartWidth, chartHeight);
        _chartInfo.ChartWidth = chartWidth;
        _chartInfo.ChartHeight = chartHeight;
        _chartInfo.ChartOffsetX = LeftMargin;
        _chartInfo.ChartOffsetY = TopMargin;
        _chartInfo.BarWidth = _barWidth;
        _chartInfo.BarSpacing = _barSpacing;

        // Calculate visible bar range
        int visibleBarCount = _chartInfo.GetVisibleBarCount();
        int lastVisibleBar = Math.Min(_firstVisibleBar + visibleBarCount, _candles.Count - 1);
        _chartInfo.UpdateVisibleRange(_firstVisibleBar, lastVisibleBar);

        // Calculate price range
        CalculatePriceRange(chartHeight);

        // Draw grid
        DrawGrid(canvas, chartWidth, chartHeight);

        // Draw candles
        DrawCandles(canvas);

        // Draw indicators
        using var renderContext = new RenderContext(canvas);
        foreach (var indicator in _activeIndicators)
        {
            // Draw DataSeries (ValueDataSeries, RangeDataSeries)
            DrawIndicatorDataSeries(canvas, indicator);
            
            // Draw custom rendering (OnRender override)
            indicator.Render(renderContext, DrawingLayouts.LatestBar);
        }

        // DEBUG: Volume histogram for PVSRA debugging
        if (SHOW_DEBUG_VOLUME)
        {
            DrawDebugVolumeHistogram(canvas, chartWidth, chartHeight);
        }

        // Draw axes
        DrawPriceAxis(canvas, info.Width, chartHeight);
        DrawTimeAxis(canvas, chartHeight, chartWidth);
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Y scale
            double delta = e.Delta > 0 ? 0.1 : -0.1;
            _yScale = Math.Clamp(_yScale + delta, 0.1, 3.0);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            // Bar width
            int delta = e.Delta > 0 ? 1 : -1;
            _barWidth = Math.Clamp(_barWidth + delta, 2, 30);
        }
        else
        {
            // Pan
            int delta = e.Delta > 0 ? -10 : 10;
            _firstVisibleBar = Math.Clamp(_firstVisibleBar + delta, 0, Math.Max(0, _candles.Count - 10));
        }
        Canvas.InvalidateVisual();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Canvas);
        var renderSize = Canvas.RenderSize;
        
        int chartWidth = (int)renderSize.Width - LeftMargin - RightMargin;
        int chartHeight = (int)renderSize.Height - TopMargin - BottomMargin;
        
        // Check if clicking on price axis (right side)
        if (position.X > renderSize.Width - RightMargin)
        {
            _dragMode = DragMode.YAxisScale;
            _panStart = position;
            _dragStartValue = _yScale;
            Canvas.CaptureMouse();
            Canvas.InvalidateVisual();
            return;
        }
        
        // Check if clicking on time axis (bottom)
        if (position.Y > TopMargin + chartHeight)
        {
            _dragMode = DragMode.XAxisScale;
            _panStart = position;
            _dragStartValue = _barWidth;
            Canvas.CaptureMouse();
            Canvas.InvalidateVisual();
            return;
        }
        
        // Chart panning
        _panStart = position;
        _panStartBar = _firstVisibleBar;
        _panStartPriceCenter = _priceCenter;
        _dragMode = DragMode.ChartPan;
        Canvas.CaptureMouse();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
        Canvas.ReleaseMouseCapture();
        Canvas.InvalidateVisual();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(Canvas);
        
        switch (_dragMode)
        {
            case DragMode.ChartPan:
                HandleChartPan(position);
                break;
            case DragMode.XAxisScale:
                HandleXAxisScale(position);
                break;
            case DragMode.YAxisScale:
                HandleYAxisScale(position);
                break;
        }
    }

    private void HandleChartPan(Point position)
    {
        // Guard against uninitialized chart
        if (_chartInfo.ChartHeight <= 0 || _chartInfo.ChartWidth <= 0)
            return;
        
        // Horizontal panning
        double deltaX = _panStart.X - position.X;
        int barDelta = (int)(deltaX / (_barWidth + _barSpacing));
        _firstVisibleBar = _panStartBar + barDelta;

        int visibleBars = _chartInfo.GetVisibleBarCount();
        int minBar = -visibleBars / 2;
        int maxBar = _candles.Count;
        _firstVisibleBar = Math.Clamp(_firstVisibleBar, minBar, maxBar);

        // Vertical panning
        double deltaY = position.Y - _panStart.Y;
        decimal priceRange = _chartInfo.PriceMax - _chartInfo.PriceMin;
        decimal pricePerPixel = priceRange / _chartInfo.ChartHeight;
        _priceCenter = _panStartPriceCenter + (decimal)deltaY * pricePerPixel;

        Canvas.InvalidateVisual();
    }

    private void HandleXAxisScale(Point position)
    {
        double deltaX = position.X - _panStart.X;
        
        // Sensitivity: 100 pixels = full range change
        double scaleFactor = deltaX / 100.0;
        int newBarWidth = (int)(_dragStartValue + scaleFactor * 10);
        
        _barWidth = Math.Clamp(newBarWidth, 2, 30);
        
        Canvas.InvalidateVisual();
    }

    private void HandleYAxisScale(Point position)
    {
        double deltaY = _panStart.Y - position.Y; // Invert for natural feel
        
        // Sensitivity: 100 pixels = 1.0 scale change
        double scaleDelta = deltaY / 100.0;
        double newScale = _dragStartValue + scaleDelta;
        
        _yScale = Math.Clamp(newScale, 0.1, 3.0);
        
        Canvas.InvalidateVisual();
    }

    #endregion

    #region Drawing Methods

    private void CalculatePriceRange(int chartHeight)
    {
        decimal minPrice = decimal.MaxValue;
        decimal maxPrice = decimal.MinValue;

        int startBar = Math.Max(0, _chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, _chartInfo.LastVisibleBarNumber);

        for (int i = startBar; i <= endBar && i < _candles.Count; i++)
        {
            var candle = _candles[i];
            if (candle.Low < minPrice) minPrice = candle.Low;
            if (candle.High > maxPrice) maxPrice = candle.High;
        }

        if (minPrice == decimal.MaxValue || maxPrice == decimal.MinValue)
        {
            minPrice = _priceCenter - 50;
            maxPrice = _priceCenter + 50;
        }

        decimal range = maxPrice - minPrice;
        decimal margin = range * 0.1m;
        decimal scaledRange = (range + margin * 2) / (decimal)_yScale;
        decimal scaledMin = _priceCenter - scaledRange / 2;
        decimal scaledMax = _priceCenter + scaledRange / 2;

        _chartInfo.SetPriceRange(scaledMin, scaledMax);
    }

    private void DrawGrid(SKCanvas canvas, int chartWidth, int chartHeight)
    {
        using var gridPaint = new SKPaint
        {
            Color = new SKColor(50, 50, 80),
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Horizontal lines
        for (int i = 1; i < 5; i++)
        {
            int y = TopMargin + (chartHeight * i / 5);
            canvas.DrawLine(LeftMargin, y, LeftMargin + chartWidth, y, gridPaint);
        }

        // Vertical lines
        int barStep = Math.Max(1, chartWidth / (_barWidth + _barSpacing) / 10);
        for (int bar = _firstVisibleBar; bar <= _chartInfo.LastVisibleBarNumber && bar < _candles.Count; bar += barStep * 10)
        {
            int x = _chartInfo.GetXByBar(bar);
            if (x >= LeftMargin && x <= LeftMargin + chartWidth)
            {
                canvas.DrawLine(x, TopMargin, x, TopMargin + chartHeight, gridPaint);
            }
        }
    }

    private void DrawCandles(SKCanvas canvas)
    {
        using var bullPaint = new SKPaint { Color = new SKColor(0, 200, 100), IsAntialias = true };
        using var bearPaint = new SKPaint { Color = new SKColor(200, 50, 50), IsAntialias = true };
        using var wickPaint = new SKPaint { Color = new SKColor(150, 150, 150), StrokeWidth = 1, IsAntialias = true };
        using var customPaint = new SKPaint { IsAntialias = true };

        int startBar = Math.Max(0, _chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, _chartInfo.LastVisibleBarNumber);

        for (int bar = startBar; bar <= endBar; bar++)
        {
            var candle = _candles[bar];
            int x = _chartInfo.GetXByBar(bar);
            int yOpen = _chartInfo.GetYByPrice(candle.Open);
            int yClose = _chartInfo.GetYByPrice(candle.Close);
            int yHigh = _chartInfo.GetYByPrice(candle.High);
            int yLow = _chartInfo.GetYByPrice(candle.Low);

            bool isBullish = candle.Close >= candle.Open;
            var paint = isBullish ? bullPaint : bearPaint;
            
            // Check for paintbars override from indicators (e.g., PVSRA)
            var paintbarColor = GetPaintbarColorForBar(bar);
            if (paintbarColor.HasValue)
            {
                var c = paintbarColor.Value;
                customPaint.Color = new SKColor(c.R, c.G, c.B, c.A);
                paint = customPaint;
            }

            // Wick
            canvas.DrawLine(x, yHigh, x, yLow, wickPaint);

            // Body
            int bodyTop = Math.Min(yOpen, yClose);
            int bodyHeight = Math.Max(1, Math.Abs(yClose - yOpen));
            int bodyWidth = Math.Max(1, _barWidth - 2);
            canvas.DrawRect(x - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight, paint);
        }
    }

    /// <summary>
    /// Check if any indicator has a PaintbarsDataSeries color for this bar
    /// </summary>
    private CrossColor? GetPaintbarColorForBar(int bar)
    {
        foreach (var indicator in _activeIndicators)
        {
            foreach (var ds in indicator.DataSeries)
            {
                if (ds is PaintbarsDataSeries paintbars && paintbars.HasColor(bar))
                {
                    var color = paintbars[bar];
                    // Check if it's not transparent/default
                    if (color.A > 0)
                    {
                        return color;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Draw all data series for an indicator
    /// </summary>
    private void DrawIndicatorDataSeries(SKCanvas canvas, Indicator indicator)
    {
        foreach (var dataSeries in indicator.DataSeries)
        {
            // Draw RangeDataSeries first (they go behind)
            if (dataSeries is RangeDataSeries rds)
            {
                DrawRangeDataSeries(canvas, rds);
            }
        }

        foreach (var dataSeries in indicator.DataSeries)
        {
            // Then draw ValueDataSeries (lines on top)
            if (dataSeries is ValueDataSeries vds)
            {
                DrawValueDataSeries(canvas, vds);
            }
        }
    }

    /// <summary>
    /// Draw ValueDataSeries as a line plot
    /// </summary>
    private void DrawValueDataSeries(SKCanvas canvas, ValueDataSeries series)
    {
        if (series.IsHidden || series.VisualType == VisualMode.Hide)
            return;

        var color = new SKColor(series.Color.R, series.Color.G, series.Color.B, series.Color.A);
        using var paint = new SKPaint
        {
            Color = color,
            StrokeWidth = series.Width,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        var path = new SKPath();
        bool started = false;

        int startBar = Math.Max(0, _chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, _chartInfo.LastVisibleBarNumber);

        for (int bar = startBar; bar <= endBar; bar++)
        {
            decimal value = series[bar];
            
            // Skip zero values if configured
            if (!series.ShowZeroValue && value == 0)
            {
                started = false;
                continue;
            }

            int x = _chartInfo.GetXByBar(bar);
            int y = _chartInfo.GetYByPrice(value);

            if (series.VisualType == VisualMode.Line)
            {
                if (!started)
                {
                    path.MoveTo(x, y);
                    started = true;
                }
                else
                {
                    path.LineTo(x, y);
                }
            }
            else if (series.VisualType == VisualMode.Dots)
            {
                canvas.DrawCircle(x, y, 2, paint);
            }
        }

        if (series.VisualType == VisualMode.Line)
        {
            canvas.DrawPath(path, paint);
        }
    }

    /// <summary>
    /// Draw RangeDataSeries as a filled area between upper and lower bounds
    /// </summary>
    private void DrawRangeDataSeries(SKCanvas canvas, RangeDataSeries series)
    {
        if (series.IsHidden)
            return;

        var color = new SKColor(
            series.RangeColor.R, 
            series.RangeColor.G, 
            series.RangeColor.B, 
            series.RangeColor.A);

        using var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        int startBar = Math.Max(0, _chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, _chartInfo.LastVisibleBarNumber);

        // Build polygon path
        var path = new SKPath();
        var upperPoints = new List<SKPoint>();
        var lowerPoints = new List<SKPoint>();

        for (int bar = startBar; bar <= endBar; bar++)
        {
            var range = series[bar];
            if (range.Upper == 0 && range.Lower == 0)
                continue;

            int x = _chartInfo.GetXByBar(bar);
            int yUpper = _chartInfo.GetYByPrice(range.Upper);
            int yLower = _chartInfo.GetYByPrice(range.Lower);

            upperPoints.Add(new SKPoint(x, yUpper));
            lowerPoints.Add(new SKPoint(x, yLower));
        }

        if (upperPoints.Count < 2)
            return;

        // Create polygon: upper points forward, then lower points backward
        path.MoveTo(upperPoints[0]);
        for (int i = 1; i < upperPoints.Count; i++)
            path.LineTo(upperPoints[i]);
        
        for (int i = lowerPoints.Count - 1; i >= 0; i--)
            path.LineTo(lowerPoints[i]);
        
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private void DrawPriceAxis(SKCanvas canvas, int width, int chartHeight)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 10,
            IsAntialias = true
        };

        decimal priceRange = _chartInfo.PriceMax - _chartInfo.PriceMin;
        decimal step = CalculatePriceStep(priceRange);
        decimal firstPrice = Math.Ceiling(_chartInfo.PriceMin / step) * step;

        for (decimal price = firstPrice; price <= _chartInfo.PriceMax; price += step)
        {
            int y = _chartInfo.GetYByPrice(price);
            if (y >= TopMargin && y <= TopMargin + chartHeight)
            {
                canvas.DrawText(price.ToString("F2"), width - RightMargin + 5, y + 4, textPaint);
            }
        }
    }

    private void DrawTimeAxis(SKCanvas canvas, int chartHeight, int chartWidth)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 10,
            IsAntialias = true
        };

        int barStep = Math.Max(1, chartWidth / (_barWidth + _barSpacing) / 6);
        
        for (int bar = _firstVisibleBar; bar <= _chartInfo.LastVisibleBarNumber && bar < _candles.Count; bar += barStep)
        {
            int x = _chartInfo.GetXByBar(bar);
            if (x >= LeftMargin && x <= LeftMargin + chartWidth)
            {
                var candle = _candles[bar];
                string label = _timeframe == Timeframe.Daily 
                    ? candle.Time.ToString("MM/dd")
                    : candle.Time.ToString("HH:mm");
                canvas.DrawText(label, x - 15, TopMargin + chartHeight + 15, textPaint);
            }
        }
    }

    private static decimal CalculatePriceStep(decimal range)
    {
        decimal step = range / 5m;
        decimal magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)step)));
        decimal normalized = step / magnitude;

        if (normalized < 1.5m) return magnitude;
        if (normalized < 3.5m) return magnitude * 2m;
        if (normalized < 7.5m) return magnitude * 5m;
        return magnitude * 10m;
    }

    // ============================================================
    // DEBUG: Volume histogram for PVSRA debugging - REMOVE LATER
    // ============================================================
    private void DrawDebugVolumeHistogram(SKCanvas canvas, int chartWidth, int chartHeight)
    {
        int histogramTop = TopMargin + chartHeight - DEBUG_VOLUME_HEIGHT;
        int histogramHeight = DEBUG_VOLUME_HEIGHT;
        
        // Draw semi-transparent background
        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 128) };
        canvas.DrawRect(LeftMargin, histogramTop, chartWidth, histogramHeight, bgPaint);
        
        // Find max volume in visible range for scaling
        decimal maxVolume = 0;
        int startBar = Math.Max(0, _chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, _chartInfo.LastVisibleBarNumber);
        
        for (int bar = startBar; bar <= endBar && bar < _candles.Count; bar++)
        {
            if (_candles[bar].Volume > maxVolume)
                maxVolume = _candles[bar].Volume;
        }
        
        if (maxVolume <= 0) return;
        
        // Draw volume bars
        using var volumePaint = new SKPaint { IsAntialias = true };
        int barWidth = Math.Max(1, _barWidth - 2);
        
        for (int bar = startBar; bar <= endBar && bar < _candles.Count; bar++)
        {
            var candle = _candles[bar];
            int x = _chartInfo.GetXByBar(bar);
            
            // Scale volume bar height
            int barHeight = (int)((double)candle.Volume / (double)maxVolume * histogramHeight);
            int barY = histogramTop + histogramHeight - barHeight;
            
            // Color based on bullish/bearish
            bool isBullish = candle.Close >= candle.Open;
            volumePaint.Color = isBullish 
                ? new SKColor(0, 180, 100, 180)  // Green with transparency
                : new SKColor(200, 50, 50, 180); // Red with transparency
            
            canvas.DrawRect(x - barWidth / 2, barY, barWidth, barHeight, volumePaint);
        }
        
        // Draw volume label
        using var labelPaint = new SKPaint { Color = SKColors.Yellow, TextSize = 9, IsAntialias = true };
        canvas.DrawText("VOLUME (DEBUG)", LeftMargin + 5, histogramTop + 12, labelPaint);
    }
    // ============================================================

    #endregion
}
