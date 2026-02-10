# Time-Based Period Data Store Specification

## Core Principle

**The data store owns period boundaries.** Don't rely on chart-specific logic like `IsNewSession`.

Period boundaries are calculated purely from timestamps:
- **4H period**: `floor(time / 4 hours)` from session start
- **Daily**: Calendar day in instrument's timezone  
- **Weekly**: ISO week number
- **Monthly**: Calendar month
- **Quarterly**: `floor((month - 1) / 3)`
- **Yearly**: Calendar year

## Completeness Model

### Current Periods (live, updating)
- **Definition**: Period that contains "now"
- **Completeness rule**: Have data from period start to `now - 2 seconds`
- **Purpose**: Provide usable answer during render cycle without millisecond precision

### Previous Periods (completed, static)
- **Definition**: Most recent completed period before current
- **Completeness rule**: Have candle data covering `[periodStart, periodEnd]`
- **Purpose**: Provide accurate high/low/open for historical reference

```
Timeline:
   |--- Previous 4H ---|--- Current 4H ---|
   ^                   ^                  ^
   must have           period             now (-2s OK)
   full coverage       boundary
```

## Data Store Architecture

```csharp
public class TimeBasedPeriodStore
{
    // Period boundary calculation - pure math, no chart dependency
    public static DateTime GetPeriodStart(PeriodType type, DateTime time, int timezoneOffset);
    public static DateTime GetPeriodEnd(PeriodType type, DateTime periodStart);
    
    // Data indexed by period key (derived from timestamp)
    private readonly Dictionary<PeriodKey, PeriodData> _periods = new();
    
    // Ingest: O(1) per candle
    public void AddCandle(DateTime time, decimal open, decimal high, decimal low, decimal close)
    {
        var key = GetPeriodKey(PeriodType.FourHour, time);
        if (!_periods.TryGetValue(key, out var period))
            _periods[key] = period = new PeriodData(key.Start);
        
        period.UpdateHighLow(high, low, time);
    }
    
    // Query: O(1) lookup
    public PeriodData? GetPrevious(PeriodType type, DateTime? asOf = null)
    {
        var now = asOf ?? DateTime.UtcNow;
        var currentStart = GetPeriodStart(type, now);
        var previousKey = new PeriodKey(type, GetPreviousPeriodStart(type, currentStart));
        return _periods.GetValueOrDefault(previousKey);
    }
    
    // Completeness: O(1) check
    public bool IsComplete(PeriodType type, PeriodKey key)
    {
        if (!_periods.TryGetValue(key, out var period))
            return false;
        return period.FirstCandleTime <= key.Start && 
               period.LastCandleTime >= key.End;
    }
}
```

## Ingest Strategy

### During OnCalculate (per bar)

```csharp
// O(1) per bar - just add to store
_store.AddCandle(candle.Time, candle.Open, candle.High, candle.Low, candle.Close);
```

No period boundary detection. No "is this a new session" checks. Just ingest.

### During OnRender (lazy query)

```csharp
// Query what we need, when we need it
var prev4h = _store.GetPrevious(PeriodType.FourHour);
if (prev4h != null && _store.IsComplete(PeriodType.FourHour, prev4h.Key))
{
    levels.Add(new KeyLevel(prev4h.High, "P4HH", color));
    levels.Add(new KeyLevel(prev4h.Low, "P4HL", color));
}
```

## Period Key Structure

```csharp
public readonly struct PeriodKey : IEquatable<PeriodKey>
{
    public PeriodType Type { get; }
    public DateTime Start { get; }
    public DateTime End { get; }
    
    public override int GetHashCode() => HashCode.Combine(Type, Start);
}
```

## Relevance Check for Real-Time Updates

When new candle data arrives:

```csharp
bool IsRelevant(DateTime candleTime, decimal high, decimal low)
{
    // 1. Always relevant if it's new data
    if (candleTime > _lastCandleTime)
        return true;
    
    // 2. For updates, only relevant if it affects current period's extremes
    var currentPeriod = _store.GetCurrent(type);
    return high > currentPeriod.High || low < currentPeriod.Low;
}
```

## Migration from Current Implementation

1. Remove all `Process*Period` methods
2. Remove all `_current*` and `_previous*` fields  
3. Remove `IsNewSession` dependency
4. Replace with single `_store.AddCandle()` call in OnCalculate
5. Update `GetDynamicLevels()` to query store instead of reading fields

## Benefits

| Aspect | Before | After |
|--------|--------|-------|
| OnCalculate per bar | 7 method calls, DateTime math | 1 store ingest, O(1) |
| Period boundary logic | Scattered across methods | Centralized in store |
| Completeness check | Manual field tracking | Store-based validation |
| Real-time relevance | Full reprocessing | O(1) check |
