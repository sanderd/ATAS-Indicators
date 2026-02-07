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
        _candles = TimeframeAggregator.GetRecentBars(_timeframe, 500);
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

    #endregion

    #region Event Handlers

    private void ChartPanel_Loaded(object sender, RoutedEventArgs e)
    {
        SymbolLabel.Text = Symbol;
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
            indicator.Render(renderContext, DrawingLayouts.LatestBar);
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
        _panStart = e.GetPosition(Canvas);
        _panStartBar = _firstVisibleBar;
        _panStartPriceCenter = _priceCenter;
        _dragMode = DragMode.ChartPan;
        Canvas.CaptureMouse();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
        Canvas.ReleaseMouseCapture();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.ChartPan)
        {
            var position = e.GetPosition(Canvas);
            
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

            // Wick
            canvas.DrawLine(x, yHigh, x, yLow, wickPaint);

            // Body
            int bodyTop = Math.Min(yOpen, yClose);
            int bodyHeight = Math.Max(1, Math.Abs(yClose - yOpen));
            int bodyWidth = Math.Max(1, _barWidth - 2);
            canvas.DrawRect(x - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight, paint);
        }
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

    #endregion
}
