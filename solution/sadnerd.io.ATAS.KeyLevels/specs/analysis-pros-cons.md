# Time-Based Period Store - Analysis

## Gap Handling Question

**Scenario**: 4H period [12:00 - 16:00], currently have:
- 12:00 - 12:45 ✓
- 13:00 - 13:30 ✓  
- 15:15 - 15:30 ✓
- 15:00 - 15:15 arrives later (backfill)

**Yes, the proposed design handles gaps naturally:**

```csharp
public void AddCandle(DateTime time, decimal high, decimal low, ...)
{
    var key = GetPeriodKey(PeriodType.FourHour, time);  // Same key regardless of arrival order
    
    if (!_periods.TryGetValue(key, out var period))
        period = _periods[key] = new PeriodData(key.Start);
    
    // Update extremes - works regardless of arrival order
    if (high > period.High) { period.High = high; period.HighTime = time; }
    if (low < period.Low) { period.Low = low; period.LowTime = time; }
    
    // Track coverage bounds
    if (time < period.EarliestCandle) period.EarliestCandle = time;
    if (time > period.LatestCandle) period.LatestCandle = time;
}
```

**Completeness check handles gaps:**
```csharp
public bool IsComplete(PeriodKey key)
{
    var period = _periods[key];
    // Have data spanning from start to end?
    return period.EarliestCandle <= key.Start && 
           period.LatestCandle >= key.End - candleDuration;
}
```

---

## Pros

| Pro | Confidence |
|-----|------------|
| **O(1) per bar** - Just dictionary lookup + comparison | High ✓ |
| **Gap tolerant** - Out-of-order data merges correctly | High ✓ |
| **Separation of concerns** - Store owns data, indicator owns display | High ✓ |
| **Testable** - Pure functions, no chart dependencies | High ✓ |
| **Lazy evaluation** - Only compute what's needed at render | High ✓ |
| **Memory efficient** - One entry per period, not per bar | High ✓ |

## Cons

| Con | Mitigation | Risk |
|-----|------------|------|
| **Clock/timezone complexity** - Period boundaries depend on instrument timezone | Use `InstrumentInfo.TimeZone` consistently | Medium |
| **Session-aware 4H** - Some markets use session-aligned 4H, not clock-aligned | Make configurable: clock vs session-based | Medium |
| **HighBar/LowBar tracking** - Need to know WHICH bar hit high/low for ray rendering | Track bar index alongside time | Low |
| **Large history** - Many periods = memory pressure | Evict old periods beyond retention window | Low |

## Potential Enhancements

1. **Period eviction policy** - Configurable retention (e.g., keep last 5 of each type)
2. **Change notification** - Event when period completes or high/low changes
3. **Snapshot/restore** - Serialize store state for faster reload
4. **Aggregation support** - Multiple charts sharing a store (already have `KeyLevelDataService`)

---

## Confidence Assessment

| Aspect | Confidence | Notes |
|--------|------------|-------|
| Core data structure | **High** | Dictionary + extremes tracking is proven pattern |
| Gap handling | **High** | Natural consequence of key-based storage |
| Completeness detection | **Medium-High** | Edge cases around session boundaries need testing |
| 4H period alignment | **Medium** | Clock-aligned is simpler; session-aligned needs more logic |
| Integration with existing code | **Medium** | Significant refactor, but clean separation |

## Finalized Decisions

1. **4H alignment**: Use session info from chart (passed to data store). Chart tells store when sessions start; store calculates 4H boundaries from there.

2. **Code cleanup**: Remove dead code (`KeyLevelDataService` if superseded, old `Process*Period` methods after migration).

3. **Retention**: Only keep **current** and **previous** period per type. Older periods are evicted - they're no longer POIs.

4. **Timestamp precision**: Most granular timestamp wins. If 5-min chart provides more precise timestamp than hourly, the precise one is kept.

## What the Data Store Tracks

| Per Period | Tracked | Notes |
|------------|---------|-------|
| High price | ✓ | The extreme value |
| High time | ✓ | Most precise timestamp available |
| High bar | ✓ | For ray rendering origin |
| Low price | ✓ | The extreme value |
| Low time | ✓ | Most precise timestamp available |
| Low bar | ✓ | For ray rendering origin |
| Open price | ✓ | First candle's open |
| Open time | ✓ | Period start |
| Period start/end | ✓ | Derived from session + period type |

