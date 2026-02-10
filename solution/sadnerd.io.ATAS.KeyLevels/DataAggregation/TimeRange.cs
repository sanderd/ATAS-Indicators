namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Represents a contiguous time segment with OHLC (Open, High, Low, Close) price data.
/// Multiple TimeRanges can be merged if they are contiguous (no time gaps).
/// </summary>
public class TimeRange
{
    /// <summary>Start time of this range (inclusive).</summary>
    public DateTime Start { get; init; }
    
    /// <summary>End time of this range (exclusive).</summary>
    public DateTime End { get; init; }
    
    /// <summary>Opening price at the start of this range.</summary>
    public decimal Open { get; init; }
    
    /// <summary>Highest price during this range.</summary>
    public decimal High { get; init; }
    
    /// <summary>Lowest price during this range.</summary>
    public decimal Low { get; init; }
    
    /// <summary>Closing price at the end of this range.</summary>
    public decimal Close { get; init; }
    
    /// <summary>Identifier of the source that contributed this range (for debugging).</summary>
    public string SourceId { get; init; } = "";

    // OHLC timestamps — when each price occurred
    public DateTime OpenTime { get; init; }
    public DateTime HighTime { get; init; }
    public DateTime LowTime { get; init; }
    public DateTime CloseTime { get; init; }
    
    /// <summary>Candle duration in minutes for the source chart. Smaller = more granular.</summary>
    public int CandleDurationMinutes { get; init; } = int.MaxValue;

    /// <summary>
    /// Checks if this range is contiguous with another range (no gap between them).
    /// Small gaps (less than 1 minute) are tolerated for timestamp alignment issues.
    /// </summary>
    public bool IsContiguousWith(TimeRange other)
    {
        if (other == null) return false;
        
        var gapThisToOther = (other.Start - End).TotalMinutes;
        if (gapThisToOther >= 0 && gapThisToOther < 1)
            return true;
        
        var gapOtherToThis = (Start - other.End).TotalMinutes;
        if (gapOtherToThis >= 0 && gapOtherToThis < 1)
            return true;
        
        if (Start < other.End && End > other.Start)
            return true;
        
        return false;
    }

    /// <summary>
    /// Merges two contiguous time ranges into a single range.
    /// Timestamps are taken from the more granular source (smaller CandleDurationMinutes).
    /// </summary>
    public static TimeRange Merge(TimeRange earlier, TimeRange later)
    {
        if (earlier == null) throw new ArgumentNullException(nameof(earlier));
        if (later == null) throw new ArgumentNullException(nameof(later));
        
        if (earlier.Start > later.Start)
            (earlier, later) = (later, earlier);
        
        // High: pick source with higher price; on tie prefer more granular
        var (mergedHigh, mergedHighTime) = PickBest(
            earlier.High, earlier.HighTime, earlier.CandleDurationMinutes,
            later.High, later.HighTime, later.CandleDurationMinutes,
            preferHigher: true);

        // Low: pick source with lower price; on tie prefer more granular
        var (mergedLow, mergedLowTime) = PickBest(
            earlier.Low, earlier.LowTime, earlier.CandleDurationMinutes,
            later.Low, later.LowTime, later.CandleDurationMinutes,
            preferHigher: false);

        // Open: from earlier; prefer more granular
        var mergedOpenTime = earlier.CandleDurationMinutes <= later.CandleDurationMinutes
            ? earlier.OpenTime : later.OpenTime;
        
        // Close: from whichever ends later
        bool laterEndsLater = later.End >= earlier.End;
        var mergedCloseTime = laterEndsLater ? later.CloseTime : earlier.CloseTime;
        
        return new TimeRange
        {
            Start = earlier.Start,
            End = later.End > earlier.End ? later.End : earlier.End,
            Open = earlier.Open,
            High = mergedHigh,
            Low = mergedLow,
            Close = laterEndsLater ? later.Close : earlier.Close,
            OpenTime = mergedOpenTime,
            HighTime = mergedHighTime,
            LowTime = mergedLowTime,
            CloseTime = mergedCloseTime,
            CandleDurationMinutes = Math.Min(earlier.CandleDurationMinutes, later.CandleDurationMinutes),
            SourceId = string.IsNullOrEmpty(earlier.SourceId) && string.IsNullOrEmpty(later.SourceId) 
                ? "" 
                : $"{earlier.SourceId}+{later.SourceId}"
        };
    }

    private static (decimal price, DateTime time) PickBest(
        decimal priceA, DateTime timeA, int durationA,
        decimal priceB, DateTime timeB, int durationB,
        bool preferHigher)
    {
        int cmp = priceA.CompareTo(priceB);
        if (cmp == 0)
            return durationA <= durationB ? (priceA, timeA) : (priceB, timeB);
        
        if (preferHigher)
            return cmp > 0 ? (priceA, timeA) : (priceB, timeB);
        else
            return cmp < 0 ? (priceA, timeA) : (priceB, timeB);
    }

    public override string ToString() =>
        $"[{Start:HH:mm}-{End:HH:mm}] O:{Open} H:{High} L:{Low} C:{Close}";
}
