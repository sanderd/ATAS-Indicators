using System.Windows;
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
/// Main window for the mock chart application
/// </summary>
public partial class MainWindow : Window
{
    #region Constants

    private const int LeftMargin = 10;
    private const int RightMargin = 80; // Space for price axis
    private const int TopMargin = 10;
    private const int BottomMargin = 30; // Space for time axis

    #endregion

    #region Fields

    private readonly List<IndicatorCandle> _candles;
    private readonly List<Indicator> _activeIndicators = new();
    private readonly ChartInfo _chartInfo;
    private readonly Container _container;

    // Available indicator types for registry
    private static readonly Dictionary<string, Type> AvailableIndicatorTypes = new()
    {
        { "Key Levels", typeof(sadnerd.io.ATAS.KeyLevels.KeyLevels) },
        { "PVSRA Candles", typeof(sadnerd.io.ATAS.PvsraCandles.PvsraCandles) },
        { "EMA with Cloud", typeof(sadnerd.io.ATAS.EmaWithCloud.EmaWithCloud) }
    };

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

    #region Constructor

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            // Generate sample data
            _candles = SampleDataGenerator.GenerateData();

            // Initialize chart info
            _chartInfo = new ChartInfo(_candles)
            {
                BarWidth = _barWidth,
                BarSpacing = _barSpacing,
                ChartOffsetX = LeftMargin,
                ChartOffsetY = TopMargin
            };

            // Initialize container
            _container = new Container();

            // Add default indicator (KeyLevels)
            AddIndicator("Key Levels");

            // Calculate initial center price
            if (_candles.Count > 0)
            {
                var lastCandle = _candles[^1];
                _priceCenter = (lastCandle.High + lastCandle.Low) / 2;
            }

            // Set initial view to show last N bars
            int visibleBars = 100;
            _firstVisibleBar = Math.Max(0, _candles.Count - visibleBars);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing: {ex.Message}\n\n{ex.StackTrace}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            throw;
        }
    }

    #endregion

    #region Indicator Management

    /// <summary>
    /// Add an indicator by name from the registry
    /// </summary>
    private void AddIndicator(string name)
    {
        if (!AvailableIndicatorTypes.TryGetValue(name, out var type))
            return;

        // Check if already active
        if (_activeIndicators.Any(i => i.GetType() == type))
            return;

        var indicator = (Indicator)Activator.CreateInstance(type)!;
        indicator.ChartInfo = _chartInfo;
        indicator.Container = _container;
        indicator.InstrumentInfo = new InstrumentInfo { TimeZone = 0, TickSize = 0.25m };
        indicator.SetCandles(_candles);
        indicator.Calculate();

        _activeIndicators.Add(indicator);
    }

    /// <summary>
    /// Remove an indicator by type
    /// </summary>
    private void RemoveIndicator(Type type)
    {
        var indicator = _activeIndicators.FirstOrDefault(i => i.GetType() == type);
        if (indicator != null)
        {
            _activeIndicators.Remove(indicator);
        }
    }

    /// <summary>
    /// Check if an indicator type is currently active
    /// </summary>
    private bool IsIndicatorActive(string name)
    {
        if (!AvailableIndicatorTypes.TryGetValue(name, out var type))
            return false;
        return _activeIndicators.Any(i => i.GetType() == type);
    }

    #endregion

    #region Chart Rendering

    private void ChartCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Clear background
        canvas.Clear(new SKColor(26, 26, 46)); // #1a1a2e

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

        // Calculate visible bar range - clamp to actual data when view extends past it
        int visibleBarCount = _chartInfo.GetVisibleBarCount();
        int lastVisibleBar = Math.Min(_firstVisibleBar + visibleBarCount, _candles.Count - 1);
        
        _chartInfo.UpdateVisibleRange(_firstVisibleBar, lastVisibleBar);

        // Calculate price range based on visible candles and Y scale
        CalculatePriceRange(chartHeight);

        // Draw grid
        DrawGrid(canvas, chartWidth, chartHeight);

        // Draw candles
        DrawCandles(canvas);

        // Draw all active indicators
        using var renderContext = new OFT.Rendering.Context.RenderContext(canvas);
        foreach (var indicator in _activeIndicators)
        {
            // Draw DataSeries (clouds, lines) first
            DrawIndicatorDataSeries(canvas, indicator, _chartInfo);
            // Then draw custom rendering
            indicator.Render(renderContext, DrawingLayouts.LatestBar);
        }

        // Draw price axis (with drag highlight)
        DrawPriceAxis(canvas, info.Width, chartHeight);

        // Draw time axis (with drag highlight)
        DrawTimeAxis(canvas, chartHeight, chartWidth);

        // Update status labels
        UpdateStatusLabels();
    }

    private void CalculatePriceRange(int chartHeight)
    {
        // Get price range from visible candles (only those that exist)
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

        if (minPrice == decimal.MaxValue)
        {
            // No visible candles, use last known price
            if (_candles.Count > 0)
            {
                var lastCandle = _candles[^1];
                minPrice = lastCandle.Low;
                maxPrice = lastCandle.High;
            }
            else
            {
                return;
            }
        }

        // Add padding
        decimal range = maxPrice - minPrice;
        if (range == 0) range = 10m; // Prevent division by zero
        decimal padding = range * 0.1m;

        // Apply Y scale (zoom)
        decimal scaledRange = range / (decimal)_yScale;
        decimal scaledPadding = padding / (decimal)_yScale;

        // Center around price center
        _chartInfo.PriceMin = _priceCenter - scaledRange / 2 - scaledPadding;
        _chartInfo.PriceMax = _priceCenter + scaledRange / 2 + scaledPadding;
    }

    private void DrawGrid(SKCanvas canvas, int chartWidth, int chartHeight)
    {
        using var gridPaint = new SKPaint
        {
            Color = new SKColor(60, 60, 90, 100),
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        // Horizontal grid lines (price levels)
        int numHLines = 10;
        for (int i = 0; i <= numHLines; i++)
        {
            int y = TopMargin + (chartHeight * i / numHLines);
            canvas.DrawLine(LeftMargin, y, LeftMargin + chartWidth, y, gridPaint);
        }

        // Vertical grid lines (time)
        int barInterval = Math.Max(1, _chartInfo.GetVisibleBarCount() / 10);
        for (int bar = _chartInfo.FirstVisibleBarNumber; bar <= _chartInfo.LastVisibleBarNumber; bar += barInterval)
        {
            if (bar >= 0) // Allow drawing grid for virtual bars
            {
                int x = _chartInfo.GetXByBar(bar);
                if (x >= LeftMargin && x <= LeftMargin + chartWidth)
                {
                    canvas.DrawLine(x, TopMargin, x, TopMargin + chartHeight, gridPaint);
                }
            }
        }
    }

    private void DrawCandles(SKCanvas canvas)
    {
        using var bullPaint = new SKPaint
        {
            Color = new SKColor(76, 175, 80), // Green
            Style = SKPaintStyle.Fill
        };
        using var bearPaint = new SKPaint
        {
            Color = new SKColor(244, 67, 54), // Red
            Style = SKPaintStyle.Fill
        };
        using var wickPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        // Only draw candles that exist
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

            bool isBull = candle.Close >= candle.Open;
            var bodyPaint = isBull ? bullPaint : bearPaint;

            // Draw wick
            wickPaint.Color = isBull ? new SKColor(76, 175, 80) : new SKColor(244, 67, 54);
            canvas.DrawLine(x, yHigh, x, yLow, wickPaint);

            // Draw body
            int bodyTop = Math.Min(yOpen, yClose);
            int bodyHeight = Math.Max(1, Math.Abs(yOpen - yClose));
            int bodyWidth = Math.Max(1, _barWidth - 2);

            canvas.DrawRect(x - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight, bodyPaint);
        }
    }

    private void DrawPriceAxis(SKCanvas canvas, int canvasWidth, int chartHeight)
    {
        // Draw axis background with highlight when dragging
        using var axisBgPaint = new SKPaint
        {
            Color = _dragMode == DragMode.YAxisScale ? new SKColor(60, 60, 100) : new SKColor(42, 42, 78),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(canvasWidth - RightMargin, TopMargin, RightMargin, chartHeight, axisBgPaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 10,
            IsAntialias = true
        };

        int numLabels = 10;
        for (int i = 0; i <= numLabels; i++)
        {
            decimal price = _chartInfo.PriceMin + ((_chartInfo.PriceMax - _chartInfo.PriceMin) * i / numLabels);
            int y = _chartInfo.GetYByPrice(price);

            // Only draw if within visible area
            if (y >= TopMargin && y <= TopMargin + chartHeight)
            {
                canvas.DrawText(price.ToString("F2"), canvasWidth - RightMargin + 5, y + 4, textPaint);
            }
        }
    }

    private void DrawTimeAxis(SKCanvas canvas, int chartHeight, int chartWidth)
    {
        // Draw axis background with highlight when dragging
        using var axisBgPaint = new SKPaint
        {
            Color = _dragMode == DragMode.XAxisScale ? new SKColor(60, 60, 100) : new SKColor(42, 42, 78),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(LeftMargin, TopMargin + chartHeight, chartWidth, BottomMargin, axisBgPaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 10,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        int barInterval = Math.Max(1, _chartInfo.GetVisibleBarCount() / 8);
        for (int bar = _chartInfo.FirstVisibleBarNumber; bar <= _chartInfo.LastVisibleBarNumber; bar += barInterval)
        {
            if (bar >= 0 && bar < _candles.Count)
            {
                int x = _chartInfo.GetXByBar(bar);
                if (x >= LeftMargin && x <= LeftMargin + chartWidth)
                {
                    var time = _candles[bar].Time;
                    string label = time.ToString("MM/dd HH:mm");
                    canvas.DrawText(label, x, TopMargin + chartHeight + 20, textPaint);
                }
            }
        }
    }

    private void UpdateStatusLabels()
    {
        VisibleBarsLabel.Content = $"{_chartInfo.FirstVisibleBarNumber} - {_chartInfo.LastVisibleBarNumber}";
        PriceRangeLabel.Content = $"{_chartInfo.PriceMin:F2} - {_chartInfo.PriceMax:F2}";
    }

    /// <summary>
    /// Gets candle color override from PaintbarsDataSeries if available
    /// </summary>
    private CrossColor? GetCandleColorOverride(int bar, Indicator indicator)
    {
        foreach (var dataSeries in indicator.DataSeries)
        {
            if (dataSeries is PaintbarsDataSeries paintbars && paintbars.HasColor(bar))
            {
                var color = paintbars[bar];
                if (color.A > 0) // Not transparent
                    return color;
            }
        }
        return null;
    }

    /// <summary>
    /// Draw ValueDataSeries as a line plot
    /// </summary>
    private void DrawValueDataSeries(SKCanvas canvas, ValueDataSeries series, ChartInfo chartInfo)
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

        int startBar = Math.Max(0, chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, chartInfo.LastVisibleBarNumber);

        for (int bar = startBar; bar <= endBar; bar++)
        {
            decimal value = series[bar];
            
            // Skip zero values if configured
            if (!series.ShowZeroValue && value == 0)
            {
                started = false;
                continue;
            }

            int x = chartInfo.GetXByBar(bar);
            int y = chartInfo.GetYByPrice(value);

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
    private void DrawRangeDataSeries(SKCanvas canvas, RangeDataSeries series, ChartInfo chartInfo)
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

        int startBar = Math.Max(0, chartInfo.FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, chartInfo.LastVisibleBarNumber);

        // Build polygon path
        var path = new SKPath();
        var upperPoints = new List<SKPoint>();
        var lowerPoints = new List<SKPoint>();

        for (int bar = startBar; bar <= endBar; bar++)
        {
            var range = series[bar];
            if (range.Upper == 0 && range.Lower == 0)
                continue;

            int x = chartInfo.GetXByBar(bar);
            int yUpper = chartInfo.GetYByPrice(range.Upper);
            int yLower = chartInfo.GetYByPrice(range.Lower);

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

    /// <summary>
    /// Draw all data series for an indicator
    /// </summary>
    private void DrawIndicatorDataSeries(SKCanvas canvas, Indicator indicator, ChartInfo chartInfo)
    {
        foreach (var dataSeries in indicator.DataSeries)
        {
            // Draw RangeDataSeries first (they go behind)
            if (dataSeries is RangeDataSeries rds)
            {
                DrawRangeDataSeries(canvas, rds, chartInfo);
            }
        }

        foreach (var dataSeries in indicator.DataSeries)
        {
            // Then draw ValueDataSeries (lines on top)
            if (dataSeries is ValueDataSeries vds)
            {
                DrawValueDataSeries(canvas, vds, chartInfo);
            }
        }
    }

    #endregion

    #region Event Handlers - Controls

    private void BarWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _barWidth = (int)e.NewValue;
        BarWidthLabel.Content = _barWidth.ToString();
        ChartCanvas.InvalidateVisual();
    }

    private void YScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _yScale = e.NewValue;
        YScaleLabel.Content = $"{_yScale:F1}x";
        ChartCanvas.InvalidateVisual();
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        _barWidth = 8;
        _yScale = 1.0;
        _firstVisibleBar = Math.Max(0, _candles.Count - 100);
        
        BarWidthSlider.Value = _barWidth;
        YScaleSlider.Value = _yScale;
        
        // Recenter price
        if (_candles.Count > 0)
        {
            var lastCandle = _candles[^1];
            _priceCenter = (lastCandle.High + lastCandle.Low) / 2;
        }
        
        ChartCanvas.InvalidateVisual();
    }

    private void AutoFit_Click(object sender, RoutedEventArgs e)
    {
        _chartInfo.AutoFitPriceRange(0.1m);
        _priceCenter = (_chartInfo.PriceMax + _chartInfo.PriceMin) / 2;
        _yScale = 1.0;
        YScaleSlider.Value = _yScale;
        ChartCanvas.InvalidateVisual();
    }

    private void PanLeft_Click(object sender, RoutedEventArgs e)
    {
        int panAmount = Math.Max(1, _chartInfo.GetVisibleBarCount() / 4);
        _firstVisibleBar = _firstVisibleBar - panAmount; // Allow going negative for empty space on left
        ChartCanvas.InvalidateVisual();
    }

    private void PanRight_Click(object sender, RoutedEventArgs e)
    {
        int panAmount = Math.Max(1, _chartInfo.GetVisibleBarCount() / 4);
        // Allow panning beyond data to show empty space on right
        _firstVisibleBar = _firstVisibleBar + panAmount;
        ChartCanvas.InvalidateVisual();
    }

    #endregion

    #region Event Handlers - Settings Panel

    private void ToggleSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        
        // Update labels
        FontSizeLabel.Content = ((int)FontSizeSlider.Value).ToString();
        LineWidthLabel.Content = ((int)LineWidthSlider.Value).ToString();
        BGWidthLabel.Content = ((int)BGWidthSlider.Value).ToString();
    }

    private void ApplySettings_Click(object sender, RoutedEventArgs e)
    {
        // Apply settings to KeyLevels indicator if active
        var keyLevels = _activeIndicators
            .OfType<sadnerd.io.ATAS.KeyLevels.KeyLevels>()
            .FirstOrDefault();

        if (keyLevels == null)
            return;

        // Apply indicator settings
        keyLevels.FontSize = (int)FontSizeSlider.Value;
        keyLevels.LineWidth = (int)LineWidthSlider.Value;
        keyLevels.BackgroundWidth = (int)BGWidthSlider.Value;
        keyLevels.UseShortLabels = ShortLabelsCheck.IsChecked ?? true;

        // 4H visibility
        keyLevels.Show4hOpen = Show4hOpenCheck.IsChecked ?? true;
        keyLevels.Show4hHighLow = Show4hHighLowCheck.IsChecked ?? true;
        keyLevels.Show4hMid = Show4hMidCheck.IsChecked ?? true;

        // Daily visibility
        keyLevels.ShowDailyOpen = ShowDailyOpenCheck.IsChecked ?? true;
        keyLevels.ShowPrevDayHighLow = ShowPrevDayHLCheck.IsChecked ?? true;
        keyLevels.ShowPrevDayMid = ShowPrevDayMidCheck.IsChecked ?? true;

        // Monday visibility
        keyLevels.ShowMondayHighLow = ShowMondayHLCheck.IsChecked ?? true;
        keyLevels.ShowMondayMid = ShowMondayMidCheck.IsChecked ?? true;

        // Quarterly visibility
        keyLevels.ShowQuarterlyOpen = ShowQuarterlyOpenCheck.IsChecked ?? true;
        keyLevels.ShowPrevQuarterHighLow = ShowPrevQuarterHLCheck.IsChecked ?? true;
        keyLevels.ShowPrevQuarterMid = ShowPrevQuarterMidCheck.IsChecked ?? true;

        // Recalculate indicator
        keyLevels.Calculate();

        ChartCanvas.InvalidateVisual();
    }

    private void IndicatorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox)
            return;

        var indicatorName = checkBox.Tag?.ToString();
        if (string.IsNullOrEmpty(indicatorName))
            return;

        if (checkBox.IsChecked == true)
        {
            AddIndicator(indicatorName);
        }
        else
        {
            if (AvailableIndicatorTypes.TryGetValue(indicatorName, out var indicatorType))
            {
                RemoveIndicator(indicatorType);
            }
        }
        
        ChartCanvas.InvalidateVisual();
    }

    private void OpenMultiChart_Click(object sender, RoutedEventArgs e)
    {
        var multiChartWindow = new MultiChartWindow();
        multiChartWindow.Show();
    }

    #endregion

    #region Event Handlers - Mouse Interaction

    private void ChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var position = e.GetPosition(ChartCanvas);
        
        // Check modifiers
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Y scale (price zoom)
            double delta = e.Delta > 0 ? 0.1 : -0.1;
            _yScale = Math.Clamp(_yScale + delta, 0.1, 3.0);
            YScaleSlider.Value = _yScale;
        }
        else
        {
            // X scale (bar width zoom)
            int delta = e.Delta > 0 ? 1 : -1;
            _barWidth = Math.Clamp(_barWidth + delta, 2, 30);
            BarWidthSlider.Value = _barWidth;
        }
        
        ChartCanvas.InvalidateVisual();
    }

    private void ChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ChartCanvas);
        var renderSize = ChartCanvas.RenderSize;

        int chartWidth = (int)renderSize.Width - LeftMargin - RightMargin;
        int chartHeight = (int)renderSize.Height - TopMargin - BottomMargin;

        // Check if clicking on price axis (right side)
        if (position.X > renderSize.Width - RightMargin)
        {
            _dragMode = DragMode.YAxisScale;
            _panStart = position;
            _dragStartValue = _yScale;
            ChartCanvas.CaptureMouse();
            ChartCanvas.InvalidateVisual();
            return;
        }

        // Check if clicking on time axis (bottom)
        if (position.Y > TopMargin + chartHeight)
        {
            _dragMode = DragMode.XAxisScale;
            _panStart = position;
            _dragStartValue = _barWidth;
            ChartCanvas.CaptureMouse();
            ChartCanvas.InvalidateVisual();
            return;
        }

        // Chart panning
        _dragMode = DragMode.ChartPan;
        _panStart = position;
        _panStartBar = _firstVisibleBar;
        _panStartPriceCenter = _priceCenter;
        ChartCanvas.CaptureMouse();
    }

    private void ChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
        ChartCanvas.ReleaseMouseCapture();
        ChartCanvas.InvalidateVisual();
    }

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(ChartCanvas);

        // Update mouse info
        if (_chartInfo != null)
        {
            int bar = _chartInfo.GetBarByX((int)position.X);
            decimal price = _chartInfo.GetPriceByY((int)position.Y);
            
            if (bar >= 0 && bar < _candles.Count)
            {
                var candle = _candles[bar];
                MouseInfoLabel.Content = $"Bar {bar} | {candle.Time:MM/dd HH:mm} | O:{candle.Open:F2} H:{candle.High:F2} L:{candle.Low:F2} C:{candle.Close:F2} | Price: {price:F2}";
            }
            else
            {
                MouseInfoLabel.Content = $"Bar: {bar} | Price: {price:F2}";
            }
        }

        // Handle dragging
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
        // Horizontal panning (bars)
        double deltaX = _panStart.X - position.X;
        int barDelta = (int)(deltaX / (_barWidth + _barSpacing));
        
        // Allow panning beyond data boundaries
        _firstVisibleBar = _panStartBar + barDelta;
        
        // Optional: clamp to reasonable range (e.g., not too far beyond data)
        int visibleBars = _chartInfo.GetVisibleBarCount();
        int minBar = -visibleBars / 2; // Allow half screen empty on left
        int maxBar = _candles.Count; // Allow full empty screen on right
        _firstVisibleBar = Math.Clamp(_firstVisibleBar, minBar, maxBar);
        
        // Vertical panning (price) - inverted for natural feel
        double deltaY = position.Y - _panStart.Y;
        decimal priceRange = _chartInfo.PriceMax - _chartInfo.PriceMin;
        decimal pricePerPixel = priceRange / _chartInfo.ChartHeight;
        _priceCenter = _panStartPriceCenter + (decimal)deltaY * pricePerPixel;
        
        ChartCanvas.InvalidateVisual();
    }

    private void HandleXAxisScale(Point position)
    {
        double deltaX = position.X - _panStart.X;
        
        // Sensitivity: 100 pixels = full range change
        double scaleFactor = deltaX / 100.0;
        int newBarWidth = (int)(_dragStartValue + scaleFactor * 10);
        
        _barWidth = Math.Clamp(newBarWidth, 2, 30);
        BarWidthSlider.Value = _barWidth;
        
        ChartCanvas.InvalidateVisual();
    }

    private void HandleYAxisScale(Point position)
    {
        double deltaY = _panStart.Y - position.Y; // Invert for natural feel
        
        // Sensitivity: 100 pixels = 1.0 scale change
        double scaleDelta = deltaY / 100.0;
        double newScale = _dragStartValue + scaleDelta;
        
        _yScale = Math.Clamp(newScale, 0.1, 3.0);
        YScaleSlider.Value = _yScale;
        
        ChartCanvas.InvalidateVisual();
    }

    #endregion
}
