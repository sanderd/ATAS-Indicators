# ATAS MockRuntime Framework

## Overview

The MockRuntime framework provides a standalone testing environment for ATAS indicators without requiring the ATAS Platform to be installed. It simulates the ATAS API surface, allowing indicators to be developed, tested, and debugged in isolation.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      MockChartApp (WPF)                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │  MainWindow     │  │  ChartCanvas    │  │  Settings UI    │  │
│  │  (SkiaSharp)    │  │  (SKGLControl)  │  │  (XAML)         │  │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘  │
│           │                    │                    │           │
│           ▼                    ▼                    ▼           │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                    Indicator Instance                       ││
│  │                 (e.g., KeyLevels.cs)                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      MockRuntime Library                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ ATAS.Indicators │  │ OFT.Rendering   │  │ Utils.Common    │  │
│  │  - Indicator    │  │  - RenderContext│  │  - Extensions   │  │
│  │  - ChartInfo    │  │  - RenderFont   │  │  - Attributes   │  │
│  │  - Container    │  │  - DrawingLayouts│ │                 │  │
│  │  - DataSeries   │  │  - RenderPen    │  │                 │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
solution/
├── sadnerd.io.ATAS.MockRuntime/       # Mock ATAS API implementation
│   ├── ATAS.Indicators.cs             # Core types: Indicator, ChartInfo, Container
│   ├── ATAS.Indicators.Technical.cs   # Technical indicators (SMA, EMA, etc.)
│   ├── OFT.Rendering.Context.cs       # RenderContext wrapping SkiaSharp
│   ├── OFT.Rendering.Settings.cs      # DrawingLayouts, RenderPen, etc.
│   ├── OFT.Rendering.Tools.cs         # RenderFont and font utilities
│   ├── OFT.Attributes.cs              # Attribute stubs
│   ├── OFT.Localization.cs            # Localization stubs
│   ├── Utils.Common.cs                # Color conversion extensions
│   └── Utils.Common.Attributes.cs     # Attribute stubs
│
├── sadnerd.io.ATAS.MockChartApp/      # WPF test application
│   ├── MainWindow.xaml(.cs)           # Main chart window with controls
│   ├── MultiChartWindow.xaml(.cs)     # Multi-pane layout testing
│   ├── SampleDataGenerator.cs         # Generates realistic OHLC data
│   └── App.xaml(.cs)                  # Application entry point
│
├── sadnerd.io.ATAS.KeyLevels/         # Indicator being tested
│   ├── KeyLevels.cs                   # Main indicator implementation
│   ├── MockGlobalUsings.cs            # Conditional usings for mock mode
│   └── sadnerd.io.ATAS.KeyLevels.csproj
│
└── Directory.Build.props              # Shared build configuration
```

## Build Configurations

### Solution Configurations

| Configuration | Description |
|---------------|-------------|
| `Debug` | Normal debug build using ATAS reference assemblies |
| `Release` | Production build using ATAS reference assemblies |
| `Debug_Mock` | Mock build - indicators use MockRuntime instead of ATAS |
| `Release_Mock` | Mock release build |

### Conditional Compilation

The `UseMockRuntime` property controls which references are used:

```xml
<!-- In Directory.Build.props -->
<PropertyGroup Condition="$(Configuration.Contains('Mock'))">
  <UseMockRuntime>true</UseMockRuntime>
</PropertyGroup>
```

```xml
<!-- In indicator .csproj files -->
<ItemGroup Condition="'$(UseMockRuntime)' == 'true'">
  <ProjectReference Include="..\sadnerd.io.ATAS.MockRuntime\sadnerd.io.ATAS.MockRuntime.csproj" />
</ItemGroup>
<ItemGroup Condition="'$(UseMockRuntime)' != 'true'">
  <Reference Include="ATAS.Indicators">
    <HintPath>..\reference-assemblies\ATAS.Indicators.dll</HintPath>
  </Reference>
  <!-- ... other ATAS references ... -->
</ItemGroup>
```

## Core Mock Types

### ChartInfo

Provides chart coordinate transformations and visible range information.

```csharp
public class ChartInfo
{
    // Visible bar range
    public int FirstVisibleBarNumber { get; }
    public int LastVisibleBarNumber { get; }
    
    // Chart dimensions
    public int ChartWidth { get; set; }
    public int ChartHeight { get; set; }
    public int ChartOffsetX { get; set; }  // Left margin
    public int ChartOffsetY { get; set; }  // Top margin
    
    // Bar sizing
    public int BarWidth { get; set; }
    public int BarSpacing { get; set; }
    
    // Price range
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    
    // Coordinate transformations
    int GetXByBar(int bar, bool clamp = true);
    int GetYByPrice(decimal price, bool clamp = true);
    decimal GetPriceByY(int y);
    int GetBarByX(int x);
}
```

**Important**: `LastVisibleBarNumber` should be clamped to actual data range when the view extends past the data:
```csharp
int lastVisibleBar = Math.Min(_firstVisibleBar + visibleBarCount, _candles.Count - 1);
```

### Container

Provides the chart region for rendering.

```csharp
public class Container
{
    public Rectangle Region { get; set; }  // System.Drawing.Rectangle
}
```

### Indicator (Base Class)

Base class that indicators inherit from.

```csharp
public abstract class Indicator
{
    // Properties set by MockChartApp
    public ChartInfo? ChartInfo { get; set; }
    public Container? Container { get; set; }
    public InstrumentInfo? InstrumentInfo { get; set; }
    
    // Convenience accessors
    protected int LastVisibleBarNumber => ChartInfo?.LastVisibleBarNumber ?? 0;
    protected int FirstVisibleBarNumber => ChartInfo?.FirstVisibleBarNumber ?? 0;
    
    // Candle data
    protected List<IndicatorCandle> SourceCandles { get; }
    
    // Lifecycle methods
    public void Calculate();              // Process all candles
    public void Render(RenderContext, DrawingLayouts);  // Render indicator
    
    // Override in derived class
    protected virtual void OnCalculate(int bar, decimal value);
    protected virtual void OnRender(RenderContext context, DrawingLayouts layout);
}
```

### RenderContext

Wraps SkiaSharp canvas for rendering.

```csharp
public class RenderContext : IDisposable
{
    public SKCanvas Canvas { get; }
    
    // Drawing methods
    void DrawLine(RenderPen pen, int x1, int y1, int x2, int y2);
    void DrawRectangle(RenderPen pen, Rectangle rect);
    void FillRectangle(Color color, Rectangle rect);
    void DrawString(string text, RenderFont font, Color color, int x, int y, StringFormat format);
    Size MeasureString(string text, RenderFont font);
    
    // Clipping
    void SetClip(Rectangle rect);
}
```

## MockChartApp Integration

### Initialization Sequence

```csharp
// 1. Generate sample data
_candles = SampleDataGenerator.GenerateData();

// 2. Create ChartInfo with candle reference
_chartInfo = new ChartInfo(_candles) {
    BarWidth = 8,
    BarSpacing = 2,
    ChartOffsetX = LeftMargin,
    ChartOffsetY = TopMargin
};

// 3. Create Container
_container = new Container();

// 4. Create and configure indicator
_indicator = new KeyLevels() {
    ChartInfo = _chartInfo,
    Container = _container,
    InstrumentInfo = new InstrumentInfo { TickSize = 0.25m }
};
_indicator.SetCandles(_candles);

// 5. IMPORTANT: Call Calculate() to process candles
_indicator.Calculate();
```

### Paint Event Updates

```csharp
void OnPaint(SKCanvas canvas)
{
    // Update chart dimensions
    _container.Region = new Rectangle(LeftMargin, TopMargin, chartWidth, chartHeight);
    _chartInfo.ChartWidth = chartWidth;
    _chartInfo.ChartHeight = chartHeight;
    
    // Update visible range (clamp to data!)
    int lastVisibleBar = Math.Min(_firstVisibleBar + visibleBarCount, _candles.Count - 1);
    _chartInfo.UpdateVisibleRange(_firstVisibleBar, lastVisibleBar);
    
    // Update price range
    _chartInfo.PriceMin = priceMin;
    _chartInfo.PriceMax = priceMax;
    
    // Render indicator
    using var context = new RenderContext(canvas);
    _indicator.Render(context, DrawingLayouts.LatestBar);
}
```

## Common Issues & Solutions

### 1. Indicator not rendering

**Symptom**: Chart shows candles but no indicator overlays.

**Check**:
- Is `_indicator.Calculate()` called after `SetCandles()`?
- Are `ChartInfo` and `Container` assigned to the indicator?
- Is `Container.Region` being updated in the paint event?

### 2. Indicator renders off-screen

**Symptom**: Levels appear at wrong X position or outside chart area.

**Check**:
- Is `LastVisibleBarNumber` clamped to actual data range?
- Is `ChartInfo.UpdateVisibleRange()` called before `Render()`?

### 3. Build configuration issues

**Symptom**: Build fails or uses wrong references.

**Check**:
- Building with correct configuration (`Debug_Mock` not `Debug`)?
- `Directory.Build.props` sets `UseMockRuntime` for Mock configurations?

## Sample Data Generator

Generates realistic OHLC data spanning multiple days/weeks for testing:

```csharp
var candles = SampleDataGenerator.GenerateData(
    daysOfData: 30,      // Number of trading days
    minutesPerBar: 5     // Timeframe
);
```

Features:
- Skips weekends automatically
- Session hours 9:30 AM - 4:00 PM
- Variable volatility (higher at open/close)
- Price bounded to reasonable range
- Fixed random seed for reproducibility

## Running the Mock App

```bash
# Build and run
cd solution
dotnet run --project sadnerd.io.ATAS.MockChartApp -c Debug_Mock

# Build only
dotnet build -c Debug_Mock sadnerd.io.ATAS.sln
```

## Extending the Framework

### Adding New Mock Types

1. Create file with appropriate namespace (e.g., `ATAS.Indicators.cs`)
2. Match ATAS API signatures exactly
3. Implement minimal functionality needed for rendering

### Adding New Indicators

1. Ensure indicator inherits from `Indicator` base class
2. Add conditional project reference in `.csproj`
3. Add to MockChartApp if needed for testing

---

## Additional Implementation Details

### IndicatorCandle Structure

```csharp
public class IndicatorCandle
{
    public DateTime Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    
    // Convenience constructor
    public IndicatorCandle(DateTime time, decimal open, decimal high, 
                           decimal low, decimal close, decimal volume);
}
```

### Coordinate System

The chart uses a **screen coordinate system** where:
- **X axis**: Left to right (bar index increases)
- **Y axis**: Top to bottom (price **decreases** as Y increases)

```
Y=0  ┌────────────────────────────┐  PriceMax
     │                            │
     │      Price decreases       │
     │           ↓                │
     │                            │
Y=H  └────────────────────────────┘  PriceMin
     X=0                        X=W
```

The `GetYByPrice` formula:
```csharp
int y = ChartOffsetY + ChartHeight - (int)(priceRatio * ChartHeight);
```

### Color Conversion (Utils.Common)

ATAS indicators use `System.Drawing.Color`, but SkiaSharp uses `SKColor`. The mock provides an extension method:

```csharp
// In Utils.Common namespace
public static class Extensions
{
    public static SKColor ToSKColor(this System.Drawing.Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
```

**Usage in indicators**:
```csharp
using Utils.Common;  // Provides ToSKColor extension

var skColor = _dailyColor.ToSKColor();
```

### InstrumentInfo

```csharp
public class InstrumentInfo
{
    public decimal TickSize { get; set; }  // e.g., 0.25 for ES futures
    public int TimeZone { get; set; }      // UTC offset in hours
}
```

### DataSeries Implementation

Minimal implementation for indicators that use data series:

```csharp
public class DataSeries
{
    private readonly List<decimal> _values = new();
    public decimal this[int index] { get; set; }
}

public class DataSeriesCollection : List<DataSeries>
{
    public DataSeries this[string name] => this[0]; // Simplified
}
```

### Technical Indicators (SMA)

```csharp
// In ATAS.Indicators.Technical namespace
public class SMA
{
    public int Period { get; set; }
    public decimal Calculate(int bar, decimal value);
}
```

### Localization Stubs

```csharp
// In OFT.Localization namespace
public static class LocalizedStrings
{
    public static string GetString(string key) => key;
}
```

### RenderFont Details

```csharp
public class RenderFont : IDisposable
{
    public string FontFamily { get; }
    public float Size { get; }
    public FontStyle Style { get; }
    public SKTypeface Typeface { get; }
    
    public RenderFont(string family, float size, FontStyle style = FontStyle.Regular);
}

public enum FontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2
}
```

### StringFormat for Text Alignment

```csharp
public enum StringFormat
{
    Default,
    Center,
    Right
}
```

### RenderPen for Lines

```csharp
public class RenderPen
{
    public Color Color { get; }
    public int Width { get; }
    public RenderPen(Color color, int width = 1);
}
```

### ATAS vs Mock API Differences

| Feature | Real ATAS | MockRuntime |
|---------|-----------|-------------|
| `GetXByBar` | Extension method on `IChart` | Direct method on `ChartInfo` |
| `GetYByPrice` | Extension method on `IChart` | Direct method on `ChartInfo` |
| Thread safety | Multi-threaded | Single-threaded |
| Real-time updates | Streaming data | Static sample data |
| Panel management | Full support | `DenyToChangePanel` only |
| Data subscriptions | Multiple timeframes | Single timeframe only |

### Period Tracking (KeyLevels-specific)

KeyLevels tracks multiple time periods. The mock must provide candles with proper `DateTime` values:

```csharp
// Periods tracked by KeyLevels
private struct PeriodData
{
    public decimal Open, High, Low, Close;
    public decimal Mid => (High + Low) / 2;
    public bool IsValid;
}

// Example periods
PeriodData _current4h, _previous4h;
PeriodData _currentDay, _previousDay;
PeriodData _currentMonday, _previousMonday;
PeriodData _currentQuarter, _previousQuarter;
```

**Important**: Sample data must span enough time to populate these periods:
- At least 2+ days for daily levels
- Span a Monday for Monday levels  
- Span quarter boundary for quarterly levels

### Settings Panel Integration

MainWindow connects UI controls to indicator properties:

```csharp
private void ApplySettings_Click(object sender, RoutedEventArgs e)
{
    _indicator.FontSize = (int)FontSizeSlider.Value;
    _indicator.LineWidth = (int)LineWidthSlider.Value;
    _indicator.BackgroundWidth = (int)BGWidthSlider.Value;
    // ... other properties
    
    ChartCanvas.InvalidateVisual(); // Force redraw
}
```

### Debugging Tips

1. **Add visual debug markers**:
   ```csharp
   // In OnRender, draw a marker to confirm rendering is called
   context.FillRectangle(Color.Red, new Rectangle(10, 10, 20, 20));
   ```

2. **Check ChartInfo state in paint event**:
   ```csharp
   Debug.WriteLine($"First={_chartInfo.FirstVisibleBarNumber}, " +
                   $"Last={_chartInfo.LastVisibleBarNumber}, " +
                   $"Width={_chartInfo.ChartWidth}");
   ```

3. **Verify period data populated**:
   ```csharp
   // In indicator, check if periods are valid
   Debug.WriteLine($"PrevDay.IsValid={_previousDay.IsValid}, " +
                   $"High={_previousDay.High}");
   ```

---

## Multi-Chart System

### Architecture

The multi-chart system provides a configurable grid layout for viewing multiple charts simultaneously.

```
┌─────────────────────────────────────────────────────────────────┐
│                    MultiChartWindow (WPF)                       │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                  Top Control Bar                            │ │
│  │  [1x1] [2x1] [2x2] [3x2]  [+Chart]  [Sync] [⚙ Settings]   │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌──────────────────────────┐ ┌──────────────────────────────┐  │
│  │      ChartPanel 1        │ │      ChartPanel 2            │  │
│  │  ES 1H                   │ │  ES 15m                      │  │
│  │  ┌────────────────────┐  │ │  ┌────────────────────────┐  │  │
│  │  │    SKElement       │  │ │  │    SKElement           │  │  │
│  │  │    (Canvas)        │  │ │  │    (Canvas)            │  │  │
│  │  └────────────────────┘  │ │  └────────────────────────┘  │  │
│  └──────────────────────────┘ └──────────────────────────────┘  │
│  ┌──────────────────────────┐ ┌───────────────────────────────┐ │
│  │      ChartPanel 3        │ │        Settings Panel         │ │
│  │  ES 4H                   │ │  Selected: ES 1H              │ │
│  │  ┌────────────────────┐  │ │  Load days: [5] ▼             │ │
│  │  │    SKElement       │  │ │  ☑ Key Levels                 │ │
│  │  │    (Canvas)        │  │ │  ☑ PVSRA Candles              │ │
│  │  └────────────────────┘  │ │  ☐ EMA with Cloud             │ │
│  └──────────────────────────┘ │  [Apply to All Charts]        │ │
│                               └───────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### ChartPanel Component

`ChartPanel` is a reusable `UserControl` that encapsulates:
- Chart canvas (SkiaSharp SKElement)
- Timeframe dropdown
- Symbol label
- Remove button
- Full mouse interaction (pan, zoom)

```csharp
public partial class ChartPanel : UserControl
{
    // Properties
    public Timeframe Timeframe { get; set; }
    public string Symbol { get; set; }
    public bool IsSelected { get; set; }
    public int DaysToLoad { get; set; }
    public IReadOnlyList<Indicator> ActiveIndicators { get; }
    
    // Events
    public event EventHandler? OnRemoveRequested;
    public event EventHandler? OnChartClicked;
    
    // Methods
    public void LoadData();
    public void AddIndicator(Indicator indicator);
    public void RemoveIndicator(Indicator indicator);
    public void RemoveIndicatorByName(string typeName);
    public void ClearIndicators();
    public void Refresh();
    public List<string> GetActiveIndicatorNames();
}
```

### Chart Selection

Only one chart can be selected at a time. The selected chart:
- Shows a highlighted border
- Has its settings displayed in the settings panel
- Is the target for indicator toggles and property changes

```csharp
private void SelectChart(ChartPanel chart)
{
    if (_selectedChart != null)
        _selectedChart.IsSelected = false;
    
    _selectedChart = chart;
    chart.IsSelected = true;
    
    if (SettingsPanel.Visibility == Visibility.Visible)
        UpdateSettingsPanelForSelectedChart();
}
```

### Mouse Interactions

ChartPanel provides comprehensive mouse interactions:

| Action | Effect |
|--------|--------|
| Click on chart | Select chart |
| Drag on chart | Pan X and Y |
| Drag on X axis (bottom) | Resize bar width |
| Drag on Y axis (right) | Resize Y scale |
| Scroll wheel | Pan horizontally |
| Ctrl + scroll | Adjust Y scale |
| Shift + scroll | Adjust bar width |

---

## Data Management

### PriceDataStore

Central store for 1-minute OHLCV data. Generates 2 years of realistic data (~500k candles) using lazy initialization.

```csharp
public static class PriceDataStore
{
    // Get full minute-level dataset (lazy loaded)
    public static List<IndicatorCandle> MinuteData { get; }
    
    // Get candles for a date range
    public static List<IndicatorCandle> GetRange(DateTime start, DateTime end);
    
    // Get most recent N days
    public static List<IndicatorCandle> GetLastDays(int days);
    
    // Reset for testing
    public static void Reset();
}
```

**Data characteristics**:
- Fixed random seed (42) for reproducibility
- Session hours: 9:30 AM - 4:00 PM EST
- Skips weekends
- Variable volatility (higher at open/close)
- Realistic volume patterns

### TimeframeAggregator

Aggregates 1-minute data into higher timeframes.

```csharp
public static class TimeframeAggregator
{
    // Aggregate minute candles to any timeframe
    public static List<IndicatorCandle> Aggregate(
        List<IndicatorCandle> minuteCandles, 
        Timeframe timeframe);
    
    // Get the most recent N bars for a timeframe
    public static List<IndicatorCandle> GetRecentBars(
        Timeframe timeframe, 
        int barCount);
}
```

### Timeframe Enum

```csharp
public enum Timeframe
{
    M1,     // 1 minute
    M5,     // 5 minutes
    M15,    // 15 minutes
    M30,    // 30 minutes
    H1,     // 1 hour
    H4,     // 4 hours
    Daily   // Daily
}

public static class TimeframeExtensions
{
    public static string ToDisplayString(this Timeframe tf);
    public static Timeframe[] CommonTimeframes { get; }  // M5, M15, H1, H4, Daily
}
```

### Days to Load

Each chart can load a configurable number of days of data:

```csharp
// In ChartPanel
public int DaysToLoad
{
    get => _daysToLoad;
    set
    {
        if (_daysToLoad != value && value > 0)
        {
            _daysToLoad = value;
            LoadData();  // Reload with new data range
        }
    }
}

// In LoadData()
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
```

---

## Data Series Rendering

### DataSeries Types

The mock runtime supports multiple data series types that indicators use:

| Type | Description | Rendering |
|------|-------------|-----------|
| `ValueDataSeries` | Single values per bar | Lines or dots |
| `RangeDataSeries` | Upper/lower bounds per bar | Filled polygon |
| `PaintbarsDataSeries` | Color per bar | Candle color override |

### ValueDataSeries

```csharp
public class ValueDataSeries : IEnumerable
{
    public string Id { get; }
    public string Name { get; }
    public CrossColor Color { get; set; }
    public VisualMode VisualType { get; set; }   // Line, Dots, Hide
    public bool IsHidden { get; set; }
    public bool ShowZeroValue { get; set; }
    public bool ShowCurrentValue { get; set; }
    public bool DrawAbovePrice { get; set; }
    public int Width { get; set; }
    
    public decimal this[int bar] { get; set; }
}
```

**Rendering** (in ChartPanel):
```csharp
private void DrawValueDataSeries(SKCanvas canvas, ValueDataSeries series)
{
    if (series.IsHidden || series.VisualType == VisualMode.Hide)
        return;
        
    var path = new SKPath();
    bool started = false;
    
    for (int bar = startBar; bar <= endBar; bar++)
    {
        decimal value = series[bar];
        if (!series.ShowZeroValue && value == 0)
        {
            started = false;
            continue;
        }
        
        int x = _chartInfo.GetXByBar(bar);
        int y = _chartInfo.GetYByPrice(value);
        
        if (series.VisualType == VisualMode.Line)
        {
            if (!started) { path.MoveTo(x, y); started = true; }
            else path.LineTo(x, y);
        }
        else if (series.VisualType == VisualMode.Dots)
        {
            canvas.DrawCircle(x, y, 2, paint);
        }
    }
    
    canvas.DrawPath(path, paint);
}
```

### RangeDataSeries

```csharp
public class RangeDataSeries : IEnumerable
{
    public string Id { get; }
    public CrossColor RangeColor { get; set; }
    public bool IsHidden { get; set; }
    public bool DrawAbovePrice { get; set; }
    
    public RangeValue this[int bar] { get; set; }
}

public struct RangeValue
{
    public decimal Upper { get; set; }
    public decimal Lower { get; set; }
}
```

**Rendering** creates a filled polygon between upper and lower bounds.

### PaintbarsDataSeries

```csharp
public class PaintbarsDataSeries : IEnumerable
{
    public CrossColor this[int bar] { get; set; }
    public bool HasColor(int bar);
}
```

**Usage**: Override candle colors (used by PVSRA indicator).

### Indicator DataSeries Rendering Order

```csharp
private void DrawIndicatorDataSeries(SKCanvas canvas, Indicator indicator)
{
    // 1. Draw RangeDataSeries first (behind)
    foreach (var ds in indicator.DataSeries)
        if (ds is RangeDataSeries rds)
            DrawRangeDataSeries(canvas, rds);
    
    // 2. Draw ValueDataSeries (lines on top)
    foreach (var ds in indicator.DataSeries)
        if (ds is ValueDataSeries vds)
            DrawValueDataSeries(canvas, vds);
}

// In Canvas_PaintSurface
foreach (var indicator in _activeIndicators)
{
    DrawIndicatorDataSeries(canvas, indicator);  // DataSeries
    indicator.Render(renderContext, DrawingLayouts.LatestBar);  // OnRender
}
```

---

## Indicator Settings

### Dynamic Property UI

The settings panel dynamically generates UI for indicator properties marked with `[Display]` attribute:

```csharp
private void GenerateIndicatorPropertiesUI()
{
    foreach (var indicator in _selectedChart.ActiveIndicators)
    {
        // Find properties with [Display] attribute
        var properties = indicator.GetType().GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(DisplayAttribute), true).Any()
                     && p.CanRead && p.CanWrite);
        
        foreach (var prop in properties)
        {
            var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
            var displayName = displayAttr?.Name ?? prop.Name;
            var value = prop.GetValue(indicator);
            
            // Create UI based on property type
            if (prop.PropertyType == typeof(bool))
            {
                // CheckBox
                var cb = new CheckBox { IsChecked = (bool?)value, Tag = (indicator, prop) };
                cb.Click += PropertyCheckBox_Click;
            }
            else if (prop.PropertyType == typeof(int))
            {
                // TextBox for integers
                var tb = new TextBox { Text = value?.ToString(), Tag = (indicator, prop) };
                tb.LostFocus += PropertyIntTextBox_LostFocus;
            }
            else if (prop.PropertyType == typeof(decimal))
            {
                // TextBox for decimals
                var tb = new TextBox { Text = value?.ToString(), Tag = (indicator, prop) };
                tb.LostFocus += PropertyDecimalTextBox_LostFocus;
            }
            else if (prop.PropertyType == typeof(CrossColor))
            {
                // Color button
                var btn = new Button { Background = colorBrush, Tag = (indicator, prop) };
            }
        }
    }
}
```

### Property Change Handlers

```csharp
private void PropertyCheckBox_Click(object sender, RoutedEventArgs e)
{
    if (sender is CheckBox cb && cb.Tag is (Indicator indicator, PropertyInfo prop))
    {
        prop.SetValue(indicator, cb.IsChecked ?? false);
        _selectedChart?.Refresh();  // Redraw chart
    }
}

private void PropertyIntTextBox_LostFocus(object sender, RoutedEventArgs e)
{
    if (sender is TextBox tb && tb.Tag is (Indicator indicator, PropertyInfo prop))
    {
        if (int.TryParse(tb.Text, out int value))
        {
            prop.SetValue(indicator, value);
            _selectedChart?.Refresh();
        }
    }
}
```

---

## Layout Persistence

### ChartLayoutManager

Saves and loads chart layout to/from JSON file.

```csharp
public static class ChartLayoutManager
{
    // Layout file is saved next to the executable
    private static string LayoutFilePath => 
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chart_layout.json");
    
    public static void SaveLayout(LayoutConfig config);
    public static LayoutConfig LoadLayout();
}
```

### Configuration Classes

```csharp
public class LayoutConfig
{
    public int Rows { get; set; }
    public int Cols { get; set; }
    public List<ChartConfig> Charts { get; set; }
}

public class ChartConfig
{
    public string Symbol { get; set; }
    public Timeframe Timeframe { get; set; }
    public int DaysToLoad { get; set; }
    public List<string> Indicators { get; set; }
    public List<IndicatorConfig> IndicatorSettings { get; set; }
}

public class IndicatorConfig
{
    public string Name { get; set; }
    public Dictionary<string, object?> Settings { get; set; }
}
```

### Saving Indicator Settings

Indicator properties with `[Display]` attribute are serialized:

```csharp
private List<IndicatorConfig> GetIndicatorSettings(ChartPanel chart)
{
    var settings = new List<IndicatorConfig>();
    
    foreach (var indicator in chart.ActiveIndicators)
    {
        var config = new IndicatorConfig { Name = indicator.GetType().Name };
        
        var properties = indicator.GetType().GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(DisplayAttribute), true).Any()
                     && p.CanRead && p.CanWrite);
        
        foreach (var prop in properties)
        {
            var value = prop.GetValue(indicator);
            if (value != null)
            {
                // Special handling for CrossColor - serialize as hex string
                if (prop.PropertyType == typeof(CrossColor))
                {
                    var color = (CrossColor)value;
                    config.Settings[prop.Name] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                }
                else
                {
                    config.Settings[prop.Name] = value;
                }
            }
        }
        
        settings.Add(config);
    }
    
    return settings;
}
```

### Loading Indicator Settings

```csharp
private void ApplyIndicatorSettings(Indicator indicator, IndicatorConfig config)
{
    foreach (var kvp in config.Settings)
    {
        var prop = indicator.GetType().GetProperty(kvp.Key);
        if (prop == null || !prop.CanWrite || kvp.Value == null)
            continue;
        
        try
        {
            if (prop.PropertyType == typeof(CrossColor) && kvp.Value is string colorStr)
            {
                // Parse from #AARRGGBB format
                if (colorStr.StartsWith("#") && colorStr.Length == 9)
                {
                    byte a = Convert.ToByte(colorStr.Substring(1, 2), 16);
                    byte r = Convert.ToByte(colorStr.Substring(3, 2), 16);
                    byte g = Convert.ToByte(colorStr.Substring(5, 2), 16);
                    byte b = Convert.ToByte(colorStr.Substring(7, 2), 16);
                    prop.SetValue(indicator, CrossColor.FromArgb(a, r, g, b));
                }
            }
            else if (prop.PropertyType == typeof(bool))
                prop.SetValue(indicator, Convert.ToBoolean(kvp.Value));
            else if (prop.PropertyType == typeof(int))
                prop.SetValue(indicator, Convert.ToInt32(kvp.Value));
            else if (prop.PropertyType == typeof(decimal))
                prop.SetValue(indicator, Convert.ToDecimal(kvp.Value));
        }
        catch { /* Ignore conversion errors */ }
    }
}
```

### Example Layout JSON

```json
{
  "Rows": 2,
  "Cols": 2,
  "Charts": [
    {
      "Symbol": "ES",
      "Timeframe": "H1",
      "DaysToLoad": 5,
      "Indicators": ["KeyLevels", "PvsraCandles"],
      "IndicatorSettings": [
        {
          "Name": "KeyLevels",
          "Settings": {
            "ShowDailyLevels": true,
            "ShowMonthlyLevels": false,
            "LineWidth": 2,
            "DailyColor": "#FF8B5CF6"
          }
        }
      ]
    },
    {
      "Symbol": "ES",
      "Timeframe": "M15",
      "DaysToLoad": 3,
      "Indicators": ["KeyLevels"]
    }
  ]
}
```

---

## Supported Indicators

### KeyLevels

Displays horizontal support/resistance levels from multiple timeframes.

**Properties with `[Display]`**:
- `ShowDailyLevels`, `ShowMondayLevels`, `ShowMonthlyLevels`, etc.
- `LineWidth`, `FontSize`, `BackgroundWidth`
- `DailyColor`, `MondayColor`, etc.

**Rendering**: Uses `OnRender` override to draw horizontal lines and labels.

### PvsraCandles

Colors candles based on Price Volume Spread Analysis.

**Uses**: `PaintbarsDataSeries` to override candle colors.

**Colors**:
- Green/Red climax bars (high volume)
- Blue/Purple rising bars (above average volume)
- Default colors for normal bars

### EmaWithCloud

Exponential Moving Average with a cloud zone around it.

**Uses**:
- `ValueDataSeries` for the EMA line
- `RangeDataSeries` for the cloud zones (upper/lower bands)

**Properties**:
- `EmaPeriod`, `EmaCloudPeriodFactor`, `EmaCloudWidthFactor`
- Color properties for line and cloud

---

## Adding New Indicators

### Step 1: Create Indicator Project

```
solution/
├── sadnerd.io.ATAS.NewIndicator/
│   ├── NewIndicator.cs
│   ├── MockGlobalUsings.cs
│   └── sadnerd.io.ATAS.NewIndicator.csproj
```

### Step 2: Conditional MockGlobalUsings.cs

```csharp
// Global usings that only apply when using MockRuntime
#if USE_MOCK_RUNTIME
global using Utils.Common;
global using OFT.Attributes;
#endif
```

### Step 3: Project File Reference

```xml
<!-- In .csproj -->
<ItemGroup Condition="'$(UseMockRuntime)' == 'true'">
  <ProjectReference Include="..\sadnerd.io.ATAS.MockRuntime\sadnerd.io.ATAS.MockRuntime.csproj" />
</ItemGroup>
<ItemGroup Condition="'$(UseMockRuntime)' != 'true'">
  <Reference Include="ATAS.Indicators">
    <HintPath>..\reference-assemblies\ATAS.Indicators.dll</HintPath>
  </Reference>
  <!-- ... other ATAS references -->
</ItemGroup>
```

### Step 4: Register in MultiChartWindow

```csharp
// In AddIndicatorToChart
switch (indicatorName)
{
    case "KeyLevels":
        chart.AddIndicator(new sadnerd.io.ATAS.KeyLevels.KeyLevels());
        break;
    case "NewIndicator":
        chart.AddIndicator(new sadnerd.io.ATAS.NewIndicator.NewIndicator());
        break;
}
```

### Step 5: Add UI Checkbox

```xml
<!-- In MultiChartWindow.xaml -->
<CheckBox x:Name="NewIndicatorCheck" Content="New Indicator" 
          Click="IndicatorToggle_Click" Tag="NewIndicator"/>
```

---

## Best Practices

### Indicator Development

1. **Use `[Display]` attribute** for properties you want exposed in the settings panel
2. **Initialize DataSeries** in the constructor with appropriate defaults
3. **Call `Calculate()`** after setting candles to populate data
4. **Use `Refresh()`** after property changes to redraw

### Mock API Compatibility

1. **Don't rely on real-time data** - MockRuntime uses static sample data
2. **Avoid multi-threading** - MockRuntime is single-threaded
3. **Test with sufficient data** - Ensure sample data spans required time periods
4. **Check null references** - `ChartInfo`, `Container` may be null during initialization

### Performance

1. **Cache paint objects** - Reuse `SKPaint` instances where possible
2. **Limit visible bar iteration** - Only process bars in visible range
3. **Use path batching** - Collect points before drawing paths
