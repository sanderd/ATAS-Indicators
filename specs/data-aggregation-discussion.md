# Data Aggregation Layer - Discussion Summary

**Date**: 2026-02-08

## Problem Statement

When running the KeyLevels indicator on multiple chart instances for the same instrument (e.g., ES-MINI on both a daily chart with 365 days history and a 5-minute chart with 2 days history), each instance only knows about the data it has access to. The 5-minute chart cannot determine yearly highs/lows because it only has 2 days of data.

## Solution Overview

Create a shared data aggregation layer that allows indicator instances to:
1. **Contribute** their processed period data (OHLC + time coverage)
2. **Query** aggregated data from all contributing instances
3. **Verify** if adequate data coverage exists for a given level

## Key Decisions from Discussion

### 1. Instrument Identification
- **Decision**: Use `InstrumentInfo.Symbol`
- **Rationale**: Clear, user-specified identifier

### 2. Project Structure
- **Decision**: No separate DLL - host all classes in the existing KeyLevels project
- **Rationale**: User preference for simplicity
- **Implementation**: Classes placed in `DataAggregation/` subfolder, written without ATAS dependencies

### 3. Data Storage
- **Decision**: Store only POI (Points of Interest), not full price history
- **Rationale**: Memory efficiency, only need OHLC per period

### 4. Coverage Detection
- **Decision**: Time-range based gap detection, not coverage ratios
- **Rationale**: Need to combine multiple timeframe sources

### 5. Combining Sources
- **Key Insight**: To answer "What is today's high?", combine:
  - Past 3 hourly candles from an hourly chart
  - Current hour's 5-minute candles from a 5-minute chart
- **Implementation**: Track contiguous time ranges, merge when adjacent

### 6. Thread Safety
- **Decision**: Use `ConcurrentDictionary`
- **Rationale**: Multiple indicator instances may access concurrently

### 7. Test Project
- **Decision**: Support mock runtime in tests
- **Rationale**: Same build patterns as indicator, no ATAS dependency for testing

## Architecture Summary

```
KeyLevelDataService (Singleton)
    └── InstrumentDataStore (per symbol)
            └── PeriodPoi (per period type + current/previous)
                    └── TimeRange[] (contributions, auto-merged)
```

## Implementation Approach

Incremental commits with builds and tests passing at each step:
1. Add `DataAggregation/` classes (TimeRange, PeriodPoi, PeriodType)
2. Add InstrumentDataStore and KeyLevelDataService
3. Add test project with unit tests
4. Integrate with KeyLevels indicator
5. Update unavailable levels logic to use aggregated coverage
