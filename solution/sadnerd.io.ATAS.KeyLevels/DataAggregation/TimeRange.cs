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

    /// <summary>
    /// Checks if this range is contiguous with another range (no gap between them).
    /// Small gaps (less than 1 minute) are tolerated for timestamp alignment issues.
    /// </summary>
    /// <param name="other">The other time range to check against.</param>
    /// <returns>True if the ranges are contiguous (can be merged).</returns>
    public bool IsContiguousWith(TimeRange other)
    {
        if (other == null) return false;
        
        // Check if this.End meets other.Start (this comes before other)
        var gapThisToOther = (other.Start - End).TotalMinutes;
        if (gapThisToOther >= 0 && gapThisToOther < 1)
            return true;
        
        // Check if other.End meets this.Start (other comes before this)
        var gapOtherToThis = (Start - other.End).TotalMinutes;
        if (gapOtherToThis >= 0 && gapOtherToThis < 1)
            return true;
        
        // Check for overlap (ranges that overlap are also considered contiguous)
        if (Start < other.End && End > other.Start)
            return true;
        
        return false;
    }

    /// <summary>
    /// Merges two contiguous time ranges into a single range.
    /// The earlier range provides the Open price, the later provides the Close.
    /// High and Low are the max/min across both ranges.
    /// </summary>
    /// <param name="earlier">The range that starts first.</param>
    /// <param name="later">The range that starts second.</param>
    /// <returns>A new TimeRange covering both input ranges.</returns>
    /// <exception cref="ArgumentException">Thrown if the ranges are not contiguous.</exception>
    public static TimeRange Merge(TimeRange earlier, TimeRange later)
    {
        if (earlier == null) throw new ArgumentNullException(nameof(earlier));
        if (later == null) throw new ArgumentNullException(nameof(later));
        
        // Ensure earlier actually comes before later
        if (earlier.Start > later.Start)
        {
            (earlier, later) = (later, earlier);
        }
        
        return new TimeRange
        {
            Start = earlier.Start,
            End = later.End > earlier.End ? later.End : earlier.End, // Take the later end
            Open = earlier.Open,
            High = Math.Max(earlier.High, later.High),
            Low = Math.Min(earlier.Low, later.Low),
            Close = later.End >= earlier.End ? later.Close : earlier.Close, // Close from whichever ends later
            SourceId = string.IsNullOrEmpty(earlier.SourceId) && string.IsNullOrEmpty(later.SourceId) 
                ? "" 
                : $"{earlier.SourceId}+{later.SourceId}"
        };
    }

    /// <summary>
    /// Returns a string representation of this time range.
    /// </summary>
    public override string ToString() =>
        $"[{Start:HH:mm}-{End:HH:mm}] O:{Open} H:{High} L:{Low} C:{Close}";
}
