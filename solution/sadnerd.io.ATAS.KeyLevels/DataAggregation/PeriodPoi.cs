using sadnerd.io.ATAS.KeyLevels.DataStore;

namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Aggregated Points of Interest (POI) for a specific time period.
/// Tracks which time ranges are covered to detect gaps in data.
/// Multiple contributions from different indicator instances are automatically merged.
/// </summary>
public class PeriodPoi
{
    private readonly object _lock = new();
    private readonly List<TimeRange> _coveredRanges = new();

    /// <summary>The type of period (Daily, Weekly, etc.).</summary>
    public PeriodType Type { get; init; }
    
    /// <summary>True if this is the current period, false if it's the previous period.</summary>
    public bool IsCurrent { get; init; }
    
    /// <summary>When this period starts.</summary>
    public DateTime PeriodStart { get; init; }
    
    /// <summary>When this period ends. Use DateTime.MaxValue for ongoing periods.</summary>
    public DateTime PeriodEnd { get; init; }

    /// <summary>Opening price at the start of the earliest covered range.</summary>
    public decimal Open { get; private set; }
    
    /// <summary>Highest price across all covered ranges.</summary>
    public decimal High { get; private set; }
    
    /// <summary>Lowest price across all covered ranges.</summary>
    public decimal Low { get; private set; }
    
    /// <summary>Closing price at the end of the latest covered range.</summary>
    public decimal Close { get; private set; }

    /// <summary>Midpoint between High and Low.</summary>
    public decimal Mid => Low + (High - Low) / 2;

    // OHLC timestamps (from most granular source) and their granularity (minutes)
    public DateTime OpenTime { get; private set; }
    public DateTime HighTime { get; private set; }
    public DateTime LowTime { get; private set; }
    public DateTime CloseTime { get; private set; }
    public int OpenTimeGranularity { get; private set; } = int.MaxValue;
    public int HighTimeGranularity { get; private set; } = int.MaxValue;
    public int LowTimeGranularity { get; private set; } = int.MaxValue;
    public int CloseTimeGranularity { get; private set; } = int.MaxValue;

    /// <summary>Read-only list of covered time ranges (sorted by Start, merged when contiguous).</summary>
    public IReadOnlyList<TimeRange> CoveredRanges
    {
        get
        {
            lock (_lock)
            {
                return _coveredRanges.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Adds a time range contribution from an indicator.
    /// The range is automatically merged with existing contiguous ranges.
    /// OHLC values are updated to reflect the aggregated data.
    /// </summary>
    /// <param name="range">The time range to add.</param>
    public void AddContribution(TimeRange range)
    {
        if (range == null) throw new ArgumentNullException(nameof(range));

        lock (_lock)
        {
            // Clip range to period boundaries
            var clippedStart = range.Start < PeriodStart ? PeriodStart : range.Start;
            var effectiveEnd = PeriodEnd == DateTime.MaxValue ? DateTime.MaxValue : PeriodEnd;
            var clippedEnd = range.End > effectiveEnd ? effectiveEnd : range.End;

            if (clippedStart >= clippedEnd)
                return; // Range is outside the period

            var clippedRange = new TimeRange
            {
                Start = clippedStart,
                End = clippedEnd,
                Open = range.Open,
                High = range.High,
                Low = range.Low,
                Close = range.Close,
                OpenTime = range.OpenTime,
                HighTime = range.HighTime,
                LowTime = range.LowTime,
                CloseTime = range.CloseTime,
                CandleDurationMinutes = range.CandleDurationMinutes,
                SourceId = range.SourceId
            };

            // Try to merge with existing ranges
            MergeIntoRanges(clippedRange);
            
            // Update OHLC from all ranges
            RecalculateOhlc();
        }
    }

    /// <summary>
    /// Checks if we have complete coverage from PeriodStart to PeriodEnd.
    /// For ongoing periods (PeriodEnd == DateTime.MaxValue), checks coverage up to the latest range end.
    /// </summary>
    /// <returns>True if there are no gaps in coverage.</returns>
    public bool HasCompleteCoverage()
    {
        lock (_lock)
        {
            if (_coveredRanges.Count == 0)
                return false;

            // For complete coverage, we need a single merged range that covers the entire period
            var first = _coveredRanges[0];
            
            // Check if first range starts at or before period start
            if (first.Start > PeriodStart)
                return false;

            // For ongoing periods, we just check if we have contiguous coverage from start
            if (PeriodEnd == DateTime.MaxValue)
            {
                // Only one contiguous range means complete coverage so far
                return _coveredRanges.Count == 1;
            }

            // For completed periods, check if coverage extends to period end
            var last = _coveredRanges[^1];
            return _coveredRanges.Count == 1 && last.End >= PeriodEnd;
        }
    }

    /// <summary>
    /// Checks if we have data coverage at a specific point in time.
    /// </summary>
    /// <param name="time">The time to check.</param>
    /// <returns>True if the time is covered by one of the ranges.</returns>
    public bool HasCoverageAt(DateTime time)
    {
        lock (_lock)
        {
            return _coveredRanges.Any(r => r.Start <= time && r.End > time);
        }
    }

    /// <summary>
    /// Returns all time gaps within the period that lack data coverage.
    /// </summary>
    /// <returns>Enumerable of (Start, End) tuples representing gaps.</returns>
    public IEnumerable<(DateTime Start, DateTime End)> GetGaps()
    {
        lock (_lock)
        {
            if (_coveredRanges.Count == 0)
            {
                var end = PeriodEnd == DateTime.MaxValue ? DateTime.UtcNow : PeriodEnd;
                yield return (PeriodStart, end);
                yield break;
            }

            // Gap before first range
            if (_coveredRanges[0].Start > PeriodStart)
            {
                yield return (PeriodStart, _coveredRanges[0].Start);
            }

            // Gaps between ranges
            for (int i = 0; i < _coveredRanges.Count - 1; i++)
            {
                var current = _coveredRanges[i];
                var next = _coveredRanges[i + 1];
                
                if (current.End < next.Start)
                {
                    yield return (current.End, next.Start);
                }
            }

            // Gap after last range (only for completed periods)
            if (PeriodEnd != DateTime.MaxValue)
            {
                var last = _coveredRanges[^1];
                if (last.End < PeriodEnd)
                {
                    yield return (last.End, PeriodEnd);
                }
            }
        }
    }

    /// <summary>
    /// Gets the total duration of all gaps within the period.
    /// </summary>
    public TimeSpan TotalGapDuration
    {
        get
        {
            return GetGaps().Aggregate(
                TimeSpan.Zero, 
                (sum, gap) => sum + (gap.End - gap.Start));
        }
    }

    private void MergeIntoRanges(TimeRange newRange)
    {
        if (_coveredRanges.Count == 0)
        {
            _coveredRanges.Add(newRange);
            return;
        }

        // Find all ranges that can be merged with the new range
        var toMerge = new List<int>();
        for (int i = 0; i < _coveredRanges.Count; i++)
        {
            if (_coveredRanges[i].IsContiguousWith(newRange))
            {
                toMerge.Add(i);
            }
        }

        if (toMerge.Count == 0)
        {
            // No overlaps, insert in sorted order
            int insertIndex = 0;
            while (insertIndex < _coveredRanges.Count && _coveredRanges[insertIndex].Start < newRange.Start)
            {
                insertIndex++;
            }
            _coveredRanges.Insert(insertIndex, newRange);
        }
        else
        {
            // Merge with all overlapping ranges
            var merged = newRange;
            foreach (var idx in toMerge.OrderByDescending(i => i))
            {
                merged = TimeRange.Merge(merged, _coveredRanges[idx]);
                _coveredRanges.RemoveAt(idx);
            }

            // Insert merged range in sorted order
            int insertIndex = 0;
            while (insertIndex < _coveredRanges.Count && _coveredRanges[insertIndex].Start < merged.Start)
            {
                insertIndex++;
            }
            _coveredRanges.Insert(insertIndex, merged);
        }
    }

    private void RecalculateOhlc()
    {
        if (_coveredRanges.Count == 0)
        {
            Open = High = Low = Close = 0;
            return;
        }

        // Sort by start time to get correct Open and Close
        var sorted = _coveredRanges.OrderBy(r => r.Start).ToList();
        var earliest = sorted[0];
        var latest = sorted.OrderByDescending(r => r.End).First();
        
        Open = earliest.Open;
        High = sorted.Max(r => r.High);
        Low = sorted.Min(r => r.Low);
        Close = latest.Close;

        // Timestamps — pick from the range with the most granular source
        // For Open: earliest range
        OpenTime = earliest.OpenTime;
        OpenTimeGranularity = earliest.CandleDurationMinutes;
        
        // For High: find the range that has the highest High, prefer more granular
        var highRange = sorted
            .Where(r => r.High == High)
            .OrderBy(r => r.CandleDurationMinutes)
            .First();
        HighTime = highRange.HighTime;
        HighTimeGranularity = highRange.CandleDurationMinutes;
        
        // For Low: find the range that has the lowest Low, prefer more granular
        var lowRange = sorted
            .Where(r => r.Low == Low)
            .OrderBy(r => r.CandleDurationMinutes)
            .First();
        LowTime = lowRange.LowTime;
        LowTimeGranularity = lowRange.CandleDurationMinutes;
        
        // For Close: latest range
        CloseTime = latest.CloseTime;
        CloseTimeGranularity = latest.CandleDurationMinutes;
    }

    /// <summary>
    /// Returns a string representation of this period POI.
    /// </summary>
    public override string ToString() =>
        $"{Type} ({(IsCurrent ? "Current" : "Previous")}): O:{Open} H:{High} L:{Low} C:{Close}, Ranges:{_coveredRanges.Count}, Complete:{HasCompleteCoverage()}";
}
