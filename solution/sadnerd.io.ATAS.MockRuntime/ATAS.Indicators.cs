using System.Drawing;
using SkiaSharp;
using OFT.Rendering.Settings;

namespace ATAS.Indicators;

/// <summary>
/// Mock IndicatorCandle matching ATAS signature
/// </summary>
public class IndicatorCandle
{
    public DateTime Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }

    public IndicatorCandle() { }

    public IndicatorCandle(DateTime time, decimal open, decimal high, decimal low, decimal close, decimal volume = 0)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}

/// <summary>
/// Mock InstrumentInfo matching ATAS signature
/// </summary>
public class InstrumentInfo
{
    public int TimeZone { get; set; }
    public decimal TickSize { get; set; } = 0.01m;
    public string Symbol { get; set; } = "MOCK";
}

/// <summary>
/// Mock ChartInfo matching ATAS signature
/// </summary>
public class ChartInfo
{
    private readonly List<IndicatorCandle> _candles;

    public int FirstVisibleBarNumber { get; private set; }
    public int LastVisibleBarNumber { get; private set; }
    public int ChartWidth { get; set; }
    public int ChartHeight { get; set; }
    public int ChartOffsetX { get; set; }
    public int ChartOffsetY { get; set; }
    public int BarWidth { get; set; } = 8;
    public int BarSpacing { get; set; } = 2;
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }

    public ChartInfo(List<IndicatorCandle> candles)
    {
        _candles = candles;
    }

    public void UpdateVisibleRange(int first, int last)
    {
        FirstVisibleBarNumber = first;
        LastVisibleBarNumber = last;
    }

    public int GetXByBar(int bar, bool clamp = true)
    {
        int relativeBar = bar - FirstVisibleBarNumber;
        int x = ChartOffsetX + (relativeBar * (BarWidth + BarSpacing)) + BarWidth / 2;

        if (clamp)
        {
            x = Math.Clamp(x, ChartOffsetX, ChartOffsetX + ChartWidth);
        }

        return x;
    }

    public int GetYByPrice(decimal price, bool clamp = true)
    {
        if (PriceMax == PriceMin) return ChartOffsetY + ChartHeight / 2;

        decimal priceRange = PriceMax - PriceMin;
        decimal priceRatio = (price - PriceMin) / priceRange;
        int y = ChartOffsetY + ChartHeight - (int)(priceRatio * ChartHeight);

        if (clamp)
        {
            y = Math.Clamp(y, ChartOffsetY, ChartOffsetY + ChartHeight);
        }

        return y;
    }

    public decimal GetPriceByY(int y)
    {
        if (ChartHeight == 0) return 0;

        decimal priceRange = PriceMax - PriceMin;
        decimal priceRatio = (decimal)(ChartOffsetY + ChartHeight - y) / ChartHeight;
        return PriceMin + (priceRatio * priceRange);
    }

    public int GetBarByX(int x)
    {
        int relativeX = x - ChartOffsetX;
        int relativeBar = relativeX / (BarWidth + BarSpacing);
        return FirstVisibleBarNumber + relativeBar;
    }

    public int GetVisibleBarCount()
    {
        if (BarWidth + BarSpacing <= 0) return 0;
        return ChartWidth / (BarWidth + BarSpacing);
    }

    public void AutoFitPriceRange(decimal padding = 0.1m)
    {
        if (_candles.Count == 0) return;

        decimal minPrice = decimal.MaxValue;
        decimal maxPrice = decimal.MinValue;

        int startBar = Math.Max(0, FirstVisibleBarNumber);
        int endBar = Math.Min(_candles.Count - 1, LastVisibleBarNumber);

        for (int i = startBar; i <= endBar; i++)
        {
            var candle = _candles[i];
            if (candle.Low < minPrice) minPrice = candle.Low;
            if (candle.High > maxPrice) maxPrice = candle.High;
        }

        if (minPrice == decimal.MaxValue) return;

        decimal range = maxPrice - minPrice;
        PriceMin = minPrice - range * padding;
        PriceMax = maxPrice + range * padding;
    }
}

/// <summary>
/// Mock Container matching ATAS signature
/// </summary>
public class Container
{
    public Rectangle Region { get; set; }
}

/// <summary>
/// Base Indicator class matching ATAS signature
/// </summary>
public abstract class Indicator
{
    protected List<IndicatorCandle> SourceCandles { get; private set; } = new();
    public ChartInfo? ChartInfo { get; set; }
    public Container? Container { get; set; }
    public InstrumentInfo? InstrumentInfo { get; set; }

    protected int CurrentBar { get; private set; }
    protected int LastVisibleBarNumber => ChartInfo?.LastVisibleBarNumber ?? 0;
    protected int FirstVisibleBarNumber => ChartInfo?.FirstVisibleBarNumber ?? 0;

    // Settings for indicator behavior
    public bool DenyToChangePanel { get; set; }
    public bool EnableCustomDrawing { get; set; }
    public bool DrawAbovePrice { get; set; }

    // Data series (minimal implementation)
    public DataSeriesCollection DataSeries { get; } = new();

    protected Indicator() { DataSeries.Add(new DataSeries()); }
    protected Indicator(bool useDefault) { if (useDefault) DataSeries.Add(new DataSeries()); }

    public void SetCandles(List<IndicatorCandle> candles)
    {
        SourceCandles = candles;
        Calculate();
    }

    public virtual void Calculate()
    {
        OnRecalculate();
        for (int i = 0; i < SourceCandles.Count; i++)
        {
            CurrentBar = i;
            OnCalculate(i, SourceCandles[i].Close);
        }
    }

    public virtual void Render(OFT.Rendering.Context.RenderContext context, OFT.Rendering.Settings.DrawingLayouts layout)
    {
        OnRender(context, layout);
    }

    protected IndicatorCandle GetCandle(int bar)
    {
        if (bar >= 0 && bar < SourceCandles.Count)
            return SourceCandles[bar];
        return new IndicatorCandle();
    }

    protected bool IsNewSession(int bar)
    {
        if (bar == 0) return true;
        var currentCandle = GetCandle(bar);
        var prevCandle = GetCandle(bar - 1);
        return currentCandle.Time.Date != prevCandle.Time.Date;
    }

    protected bool IsNewWeek(int bar)
    {
        if (bar == 0) return false;
        var currentCandle = GetCandle(bar);
        var prevCandle = GetCandle(bar - 1);
        
        var currentDay = currentCandle.Time.DayOfWeek;
        var prevDay = prevCandle.Time.DayOfWeek;
        
        return currentDay == DayOfWeek.Monday && prevDay != DayOfWeek.Monday;
    }

    /// <summary>
    /// Triggers a recalculate of indicator values (ATAS compatibility method)
    /// </summary>
    protected void RecalculateValues()
    {
        // In the mock runtime, this just triggers a recalculate
        // In real ATAS, this would schedule a recalculation on the UI thread
        // For the mock, we do nothing here since Calculate() is called manually
    }

    /// <summary>
    /// Subscribe to drawing events (ATAS compatibility method)
    /// </summary>
    protected void SubscribeToDrawingEvents(DrawingLayouts layout)
    {
        // In mock runtime, this is a no-op
        // In real ATAS, this subscribes to specific drawing phase events
    }

    // Abstract methods to be overridden
    protected virtual void OnRecalculate() { }
    protected virtual void OnCalculate(int bar, decimal value) { }
    protected virtual void OnRender(OFT.Rendering.Context.RenderContext context, OFT.Rendering.Settings.DrawingLayouts layout) { }
}

/// <summary>
/// Simple DataSeries implementation
/// </summary>
public class DataSeries
{
    private readonly Dictionary<int, decimal> _values = new();
    public bool IsHidden { get; set; }
    public string Name { get; set; } = "";

    public decimal this[int index]
    {
        get => _values.TryGetValue(index, out var val) ? val : 0;
        set => _values[index] = value;
    }

    public void Clear() => _values.Clear();
}

/// <summary>
/// Visual modes for ValueDataSeries
/// </summary>
public enum VisualMode
{
    Hide,
    Line,
    Histogram,
    Dots,
    Block,
    UpDownBlock,
    Hash,
    Square,
    Cross,
    Triangle,
    OnlyValueOnAxis
}

/// <summary>
/// Value data series for line/histogram plots
/// </summary>
public class ValueDataSeries : DataSeries
{
    private readonly Dictionary<int, decimal> _values = new();
    
    public CrossColor Color { get; set; } = CrossColors.Blue;
    public VisualMode VisualType { get; set; } = VisualMode.Line;
    public bool DrawAbovePrice { get; set; }
    public bool ShowCurrentValue { get; set; }
    public bool ShowZeroValue { get; set; } = true;
    public bool ShowTooltip { get; set; } = true;
    public int Width { get; set; } = 1;
    public int LineDashStyle { get; set; } = 0;
    
    public ValueDataSeries(string id, string name)
    {
        Name = name;
    }

    public new decimal this[int index]
    {
        get => _values.TryGetValue(index, out var val) ? val : 0;
        set => _values[index] = value;
    }

    public new void Clear() => _values.Clear();
    
    public IEnumerable<int> GetBars() => _values.Keys;
}

/// <summary>
/// Range value for RangeDataSeries
/// </summary>
public struct RangeValue
{
    public decimal Upper { get; set; }
    public decimal Lower { get; set; }

    public RangeValue(decimal upper, decimal lower)
    {
        Upper = upper;
        Lower = lower;
    }
}

/// <summary>
/// Range data series for cloud/band plots
/// </summary>
public class RangeDataSeries : DataSeries
{
    private readonly Dictionary<int, RangeValue> _values = new();
    
    public CrossColor RangeColor { get; set; } = CrossColor.FromArgb(50, 0, 0, 255);
    public string DescriptionKey { get; set; } = "";
    public bool DrawAbovePrice { get; set; }
    
    public RangeDataSeries(string id, string name)
    {
        Name = name;
    }

    public new RangeValue this[int index]
    {
        get => _values.TryGetValue(index, out var val) ? val : new RangeValue();
        set => _values[index] = value;
    }

    public new void Clear() => _values.Clear();
    
    public IEnumerable<int> GetBars() => _values.Keys;
}

/// <summary>
/// Paintbars data series for candle color overrides
/// </summary>
public class PaintbarsDataSeries : DataSeries
{
    private readonly Dictionary<int, CrossColor> _colors = new();
    
    public PaintbarsDataSeries(string id, string name)
    {
        Name = name;
    }

    public new CrossColor this[int index]
    {
        get => _colors.TryGetValue(index, out var color) ? color : CrossColors.Transparent;
        set => _colors[index] = value;
    }

    public new void Clear() => _colors.Clear();
    
    public bool HasColor(int bar) => _colors.ContainsKey(bar);
    
    public IEnumerable<int> GetBars() => _colors.Keys;
}

/// <summary>
/// Collection of data series
/// </summary>
public class DataSeriesCollection : List<DataSeries>
{
}
