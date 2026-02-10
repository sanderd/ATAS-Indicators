using sadnerd.io.ATAS.KeyLevels.DataStore;

namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Aggregated Points of Interest (POI) for a specific time period.
/// Stores OHLC data with per-value timestamps and granularity tracking.
/// Updated directly from individual bars — no TimeRange merging.
/// </summary>
public class PeriodPoi
{
    /// <summary>The type of period (Daily, Weekly, etc.).</summary>
    public PeriodType Type { get; init; }
    
    /// <summary>True if this is the current period, false if it's the previous period.</summary>
    public bool IsCurrent { get; init; }
    
    /// <summary>When this period starts (session-aligned).</summary>
    public DateTime PeriodStart { get; set; }
    
    /// <summary>When this period ends. Use DateTime.MaxValue for ongoing periods.</summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>Opening price at the start of the period.</summary>
    public decimal Open { get; private set; }
    
    /// <summary>Highest price across all bars in the period.</summary>
    public decimal High { get; private set; }
    
    /// <summary>Lowest price across all bars in the period.</summary>
    public decimal Low { get; private set; }
    
    /// <summary>Closing price of the latest bar in the period.</summary>
    public decimal Close { get; private set; }
    
    /// <summary>Midpoint between High and Low.</summary>
    public decimal Mid => Low + (High - Low) / 2;

    // OHLC timestamps (from the bar that set each value)
    public DateTime OpenTime { get; private set; }
    public DateTime HighTime { get; private set; }
    public DateTime LowTime { get; private set; }
    public DateTime CloseTime { get; private set; }
    
    // Granularity of the bar that set each OHLC value (in minutes, smaller = more precise)
    public int OpenTimeGranularity { get; private set; } = int.MaxValue;
    public int HighTimeGranularity { get; private set; } = int.MaxValue;
    public int LowTimeGranularity { get; private set; } = int.MaxValue;
    public int CloseTimeGranularity { get; private set; } = int.MaxValue;

    /// <summary>Whether this POI has been initialized with at least one bar.</summary>
    public bool IsInitialized { get; private set; }
    
    /// <summary>The time of the most recent bar that updated this POI.</summary>
    public DateTime LatestBarTime { get; private set; }

    /// <summary>
    /// Initialize this POI with the first bar of the period.
    /// Sets all OHLC values, timestamps, and granularity.
    /// </summary>
    public void Initialize(DateTime barTime, int candleDurationMinutes, decimal open, decimal high, decimal low, decimal close)
    {
        Open = open;
        High = high;
        Low = low;
        Close = close;

        OpenTime = barTime;
        HighTime = barTime;
        LowTime = barTime;
        CloseTime = barTime;

        OpenTimeGranularity = candleDurationMinutes;
        HighTimeGranularity = candleDurationMinutes;
        LowTimeGranularity = candleDurationMinutes;
        CloseTimeGranularity = candleDurationMinutes;

        LatestBarTime = barTime;
        IsInitialized = true;
    }

    /// <summary>
    /// Update this POI with a subsequent bar.
    /// High/Low update only if the new price is more extreme.
    /// Timestamps and granularity are recorded for each value that changes.
    /// On equal price, a more granular source (smaller candleDuration) wins.
    /// </summary>
    public void UpdateBar(DateTime barTime, int candleDurationMinutes, decimal high, decimal low, decimal close)
    {
        // High: update if higher price, or same price but more granular
        if (high > High || (high == High && candleDurationMinutes < HighTimeGranularity))
        {
            High = high;
            HighTime = barTime;
            HighTimeGranularity = candleDurationMinutes;
        }

        // Low: update if lower price, or same price but more granular
        if (low < Low || (low == Low && candleDurationMinutes < LowTimeGranularity))
        {
            Low = low;
            LowTime = barTime;
            LowTimeGranularity = candleDurationMinutes;
        }

        // Close: always update to latest bar
        Close = close;
        CloseTime = barTime;
        CloseTimeGranularity = candleDurationMinutes;

        if (barTime > LatestBarTime)
            LatestBarTime = barTime;
    }

    public override string ToString() =>
        $"{Type} ({(IsCurrent ? "Current" : "Previous")}): O={Open} H={High} L={Low} C={Close}";
}
