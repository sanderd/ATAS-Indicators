# KeyLevels Indicator - Technical Overview

## Purpose
The KeyLevels indicator displays important price levels on ATAS charts, including levels derived from multiple timeframes: 4-hour, daily, weekly (Monday), and quarterly periods.

## Location
`E:\Projects\sadnerd.io.indicators-github\solution\sadnerd.io.ATAS.KeyLevels\KeyLevels.cs`

## Architecture

### Core Components

#### 1. PeriodRange Class
Tracks OHLC (Open/High/Low/Close) for each time period:
```csharp
internal class PeriodRange {
    public decimal Open, High, Low, Close { get; set; }
    public decimal Mid => Low + (High - Low) / 2;
    public DateTime StartTime { get; set; }
    public int StartBar { get; set; } = -1;
    public bool IsValid => StartBar >= 0;
}
```

#### 2. KeyLevel Class
Represents a single drawable level:
```csharp
public class KeyLevel {
    public decimal Price { get; set; }
    public string Label { get; set; }
    public CrossColor Color { get; set; }
}
```

### Period Tracking System

Each period type maintains **current** and **previous** `PeriodRange` instances:

| Period | Current | Previous | Detection Method |
|--------|---------|----------|------------------|
| 4-Hour | `_current4h` | `_previous4h` | Hour rounding to 4h boundaries |
| Daily | `_currentDay` | `_previousDay` | `IsNewSession(bar)` |
| Monday | `_currentMonday` | `_previousMonday` | `IsNewWeek(bar)` + Monday check |
| Quarterly | `_currentQuarter` | `_previousQuarter` | Month/3 calculation |

### Processing Flow

```
OnCalculate(bar, value)
    ├── GetCandle(bar)
    ├── Process4HourPeriod(bar, candle)
    ├── ProcessDailyPeriod(bar, candle)
    ├── ProcessMondayPeriod(bar, candle)
    └── ProcessQuarterlyPeriod(bar, candle)
```

Each processor:
1. Detects if a new period started
2. Copies current → previous if transitioning
3. Initializes new current period
4. Updates current period with candle high/low

### Rendering Pipeline

```
OnRender(context, layout)
    ├── CalculateAnchorX(region)      // Left, Right, or LastBar
    ├── Draw background rectangle
    ├── GetDynamicLevels()            // Collect all levels
    └── DrawLevel() for each level    // Line + text label
```

Uses `DrawingLayouts.LatestBar` for efficient rendering.

## Levels Displayed

| Level | Short Label | Full Label | Color Property |
|-------|-------------|------------|----------------|
| Previous 4H High | P4HH | Prev 4H High | FourHourColor |
| Previous 4H Low | P4HL | Prev 4H Low | FourHourColor |
| 4H Open | 4HO | 4H Open | FourHourColor |
| Day Open | DO | Day Open | DailyColor |
| Previous Day High | PDH | Prev Day High | DailyColor |
| Previous Day Low | PDL | Prev Day Low | DailyColor |
| Previous Day Mid | PDM | Prev Day Mid | DailyColor |
| Monday High | MDAYH | Mon High | MondayColor |
| Monday Low | MDAYL | Mon Low | MondayColor |
| Monday Mid | MDAYM | Mon Mid | MondayColor |
| Quarter Open | QO | Quarter Open | QuarterlyColor |
| Previous Quarter High | PQH | Prev Quarter High | QuarterlyColor |
| Previous Quarter Low | PQL | Prev Quarter Low | QuarterlyColor |
| Previous Quarter Mid | PQM | Prev Quarter Mid | QuarterlyColor |

## Configurable Properties

### Drawing Group
- `FontSize` (6-24)
- `Anchor` (Left, Right, LastBar)
- `DistanceFromAnchor` (-500 to 500)
- `TextColor`
- `BackgroundColor`
- `LineWidth` (10-200)
- `BackgroundWidth` (50-500)
- `UseShortLabels` (checkbox, default: true)

### Level Colors Group
- `FourHourColor` (default: Amber)
- `DailyColor` (default: Blue)
- `MondayColor` (default: Purple)
- `QuarterlyColor` (default: Green)

## Extension Patterns

### Adding a New Period Type
1. Add `PeriodRange` fields (current + previous)
2. Add tracking variables (last period marker)
3. Add color field and property
4. Add reset logic in `OnRecalculate()`
5. Create `ProcessXxxPeriod()` method
6. Call processor in `OnCalculate()`
7. Add levels in `GetDynamicLevels()`

### Adding a New Level to Existing Period
Modify `GetDynamicLevels()` to add new `KeyLevel` entries.

## Build Dependencies
- `System.Drawing.Common` (8.0.0) - For Rectangle, StringAlignment
- ATAS reference assemblies via `ReferenceAssemblies\generated\`

## Key ATAS Base Class Methods Used
- `IsNewSession(bar)` - Daily session detection
- `IsNewWeek(bar)` - Weekly transition detection
- `GetCandle(bar)` - Get OHLC data
- `ChartInfo.GetYByPrice()` - Price to Y coordinate
- `ChartInfo.GetXByBar()` - Bar to X coordinate
