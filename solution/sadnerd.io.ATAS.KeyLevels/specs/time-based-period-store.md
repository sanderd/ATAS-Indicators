# Time-Based Period Store - Complete Specification

## Problem Statement

The current `OnCalculate` implementation processes 7 period types for **every bar**. On a 1-min chart, ~99.6% of bars don't trigger period transitions, yet we run full processing logic for each.

## Proposed Solution: TimeBasedPeriodStore

A centralized data store that:
- **Owns period boundaries** (pure timestamp math, no `IsNewSession` dependency)
- **O(1) ingest per bar** via `AddCandle()`
- **Lazy query at render time** via `GetCurrent()`/`GetPrevious()`
- **Session-aligned 4H** using chart-provided session info

## What the Store Tracks

| Per Period | Tracked | Notes |
|------------|---------|-------|
| High price | ✓ | The extreme value |
| High time | ✓ | Most precise timestamp available |
| High bar | ✓ | For ray rendering origin |
| Low price | ✓ | The extreme value |
| Low time | ✓ | Most precise timestamp available |
| Low bar | ✓ | For ray rendering origin |
| Open price | ✓ | First candle's open |
| Period start/end | ✓ | Derived from session + period type |

## Retention Policy

Only **current** and **previous** period per type. Older periods are evicted - they're no longer POIs.

## Completeness Rules

- **Current periods**: Have data from period start to `now - 2 seconds`
- **Previous periods**: Have candle data covering `[periodStart, periodEnd]`

## Gap Handling

Data arriving out-of-order merges correctly because storage is keyed by period, not arrival order. Backfilled data updates high/low if it exceeds current values.

## Timestamp Precision

Most granular timestamp wins. If 5-min chart reports high at 14:23 and hourly reports at 14:00, the 14:23 is kept.

---

## Pros

| Pro | Confidence |
|-----|------------|
| **O(1) per bar** - Dictionary lookup + comparison | High ✓ |
| **Gap tolerant** - Out-of-order data merges correctly | High ✓ |
| **Separation of concerns** - Store owns data, indicator owns display | High ✓ |
| **Testable** - Pure functions, no chart dependencies | High ✓ |
| **Lazy evaluation** - Only compute what's needed at render | High ✓ |
| **Memory efficient** - Two entries per period type max | High ✓ |

## Cons

| Con | Mitigation | Risk |
|-----|------------|------|
| **Session alignment** - 4H depends on session start | Chart provides session info | Low |
| **HighBar/LowBar tracking** - Need bar index for rays | Track alongside timestamp | Low |
| **Refactor scope** - Replace 7 methods + fields | Clean migration path | Medium |

---

## Implementation Details

### Ingest (OnCalculate)
```csharp
_store.AddCandle(candle.Time, bar, candle.Open, candle.High, candle.Low, candle.Close);
```

### Query (GetDynamicLevels)
```csharp
var prev4h = _store.GetPrevious(PeriodType.FourHour);
if (prev4h != null)
{
    levels.Add(new KeyLevel(prev4h.High, "P4HH", color, prev4h.HighTime, prev4h.HighBar));
}
```

### Relevance Check (Real-Time)
```csharp
bool IsRelevant(decimal high, decimal low)
{
    return high > _store.GetCurrent(type).High || low < _store.GetCurrent(type).Low;
}
```
