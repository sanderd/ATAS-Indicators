namespace sadnerd.io.ATAS.KeyLevels.DataStore;

/// <summary>
/// Period types tracked by the data store
/// </summary>
public enum PeriodType
{
    FourHour,
    Daily,
    Monday,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>
/// Tracks OHLC data and timing information for a single period
/// </summary>
public class PeriodData
{
    public DateTime PeriodStart { get; }
    public DateTime PeriodEnd { get; }
    
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public decimal Mid => Low + (High - Low) / 2;
    
    // Timing information for ray rendering
    public DateTime HighTime { get; private set; }
    public DateTime LowTime { get; private set; }
    public int HighBar { get; private set; } = -1;
    public int LowBar { get; private set; } = -1;
    public int StartBar { get; private set; } = -1;
    
    // Coverage tracking
    public DateTime EarliestCandle { get; private set; } = DateTime.MaxValue;
    public DateTime LatestCandle { get; private set; } = DateTime.MinValue;
    public int CandleCount { get; private set; }
    
    public PeriodData(DateTime periodStart, DateTime periodEnd)
    {
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        High = decimal.MinValue;
        Low = decimal.MaxValue;
    }
    
    /// <summary>
    /// Initialize with first candle data
    /// </summary>
    public void Initialize(DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        Open = open;
        High = high;
        Low = low;
        Close = close;
        HighTime = time;
        LowTime = time;
        HighBar = bar;
        LowBar = bar;
        StartBar = bar;
        EarliestCandle = time;
        LatestCandle = time;
        CandleCount = 1;
    }
    
    /// <summary>
    /// Update with new candle data - O(1) operation
    /// </summary>
    public void Update(DateTime time, int bar, decimal high, decimal low, decimal close)
    {
        if (high > High)
        {
            High = high;
            HighTime = time;
            HighBar = bar;
        }
        if (low < Low)
        {
            Low = low;
            LowTime = time;
            LowBar = bar;
        }
        Close = close;
        
        if (time < EarliestCandle) EarliestCandle = time;
        if (time > LatestCandle) LatestCandle = time;
        CandleCount++;
    }
    
    /// <summary>
    /// Check if we have data covering the period bounds
    /// </summary>
    public bool HasCompleteCoverage(TimeSpan candleDuration)
    {
        return EarliestCandle <= PeriodStart && 
               LatestCandle >= PeriodEnd - candleDuration;
    }
    
    public bool IsInitialized => CandleCount > 0;
}
