using System.Diagnostics;
using Utils.Common.Logging;

namespace sadnerd.io.ATAS.KeyLevels.DataStore;

/// <summary>
/// Time-based period store with O(1) ingest and lazy query.
/// Owns period boundary calculation - no chart/session dependency beyond timezone.
/// </summary>
public class TimeBasedPeriodStore
{
    private readonly int _timezoneOffset;
    private DateTime _sessionStart;
    
    // Current + Previous per period type (older periods evicted)
    private readonly Dictionary<PeriodType, PeriodData?> _current = new();
    private readonly Dictionary<PeriodType, PeriodData?> _previous = new();
    
    // Diagnostics
    private readonly Stopwatch _ingestStopwatch = new();
    private long _totalIngestCalls;
    private long _totalIngestTicks;
    
    public TimeBasedPeriodStore(int timezoneOffset = 0)
    {
        _timezoneOffset = timezoneOffset;
        foreach (PeriodType type in Enum.GetValues<PeriodType>())
        {
            _current[type] = null;
            _previous[type] = null;
        }
    }
    
    /// <summary>
    /// Set session start time (from chart info)
    /// </summary>
    public void SetSessionStart(DateTime sessionStart)
    {
        _sessionStart = sessionStart;
        this.LogDebug($"Session start set to {sessionStart:HH:mm}");
    }
    
    /// <summary>
    /// O(1) ingest per candle - the hot path
    /// </summary>
    public void AddCandle(DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        _ingestStopwatch.Restart();
        
        var adjustedTime = time.AddHours(_timezoneOffset);
        
        // Process each period type
        AddCandleToPeriod(PeriodType.FourHour, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Daily, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Monday, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Weekly, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Monthly, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Quarterly, adjustedTime, bar, open, high, low, close);
        AddCandleToPeriod(PeriodType.Yearly, adjustedTime, bar, open, high, low, close);
        
        _ingestStopwatch.Stop();
        _totalIngestTicks += _ingestStopwatch.ElapsedTicks;
        _totalIngestCalls++;
    }
    
    private void AddCandleToPeriod(PeriodType type, DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        var (periodStart, periodEnd) = GetPeriodBounds(type, time);
        
        // Skip if this period type doesn't apply (e.g., Monday only on Mondays)
        if (periodStart == DateTime.MinValue)
            return;
        
        var current = _current[type];
        
        // Check if we've transitioned to a new period
        if (current == null || time >= current.PeriodEnd)
        {
            // Transition: current becomes previous
            if (current != null && current.IsInitialized)
            {
                _previous[type] = current;
            }
            
            // Create new current period
            current = new PeriodData(periodStart, periodEnd);
            current.Initialize(time, bar, open, high, low, close);
            _current[type] = current;
        }
        else if (time >= current.PeriodStart)
        {
            // Update existing period
            current.Update(time, bar, high, low, close);
        }
    }
    
    /// <summary>
    /// Get current period (may be incomplete)
    /// </summary>
    public PeriodData? GetCurrent(PeriodType type) => _current[type];
    
    /// <summary>
    /// Get previous (completed) period
    /// </summary>
    public PeriodData? GetPrevious(PeriodType type) => _previous[type];
    
    /// <summary>
    /// Calculate period boundaries for a given time
    /// </summary>
    private (DateTime start, DateTime end) GetPeriodBounds(PeriodType type, DateTime time)
    {
        return type switch
        {
            PeriodType.FourHour => GetFourHourBounds(time),
            PeriodType.Daily => GetDailyBounds(time),
            PeriodType.Monday => GetMondayBounds(time),
            PeriodType.Weekly => GetWeeklyBounds(time),
            PeriodType.Monthly => GetMonthlyBounds(time),
            PeriodType.Quarterly => GetQuarterlyBounds(time),
            PeriodType.Yearly => GetYearlyBounds(time),
            _ => (DateTime.MinValue, DateTime.MinValue)
        };
    }
    
    private (DateTime start, DateTime end) GetFourHourBounds(DateTime time)
    {
        // Session-aligned: 4H periods from session start
        var sessionDate = _sessionStart != DateTime.MinValue ? _sessionStart : time.Date;
        var hoursSinceSession = (time - sessionDate).TotalHours;
        var periodIndex = (int)(hoursSinceSession / 4);
        var start = sessionDate.AddHours(periodIndex * 4);
        return (start, start.AddHours(4));
    }
    
    private (DateTime start, DateTime end) GetDailyBounds(DateTime time)
    {
        var start = time.Date;
        return (start, start.AddDays(1));
    }
    
    private (DateTime start, DateTime end) GetMondayBounds(DateTime time)
    {
        if (time.DayOfWeek != DayOfWeek.Monday)
            return (DateTime.MinValue, DateTime.MinValue);
        
        var start = time.Date;
        return (start, start.AddDays(1));
    }
    
    private (DateTime start, DateTime end) GetWeeklyBounds(DateTime time)
    {
        var daysSinceMonday = ((int)time.DayOfWeek + 6) % 7;
        var start = time.Date.AddDays(-daysSinceMonday);
        return (start, start.AddDays(7));
    }
    
    private (DateTime start, DateTime end) GetMonthlyBounds(DateTime time)
    {
        var start = new DateTime(time.Year, time.Month, 1);
        return (start, start.AddMonths(1));
    }
    
    private (DateTime start, DateTime end) GetQuarterlyBounds(DateTime time)
    {
        var quarter = (time.Month - 1) / 3;
        var start = new DateTime(time.Year, quarter * 3 + 1, 1);
        return (start, start.AddMonths(3));
    }
    
    private (DateTime start, DateTime end) GetYearlyBounds(DateTime time)
    {
        var start = new DateTime(time.Year, 1, 1);
        return (start, start.AddYears(1));
    }
    
    /// <summary>
    /// Log diagnostic summary
    /// </summary>
    public void LogDiagnostics(object source)
    {
        if (_totalIngestCalls == 0) return;
        
        var avgMicroseconds = (_totalIngestTicks * 1_000_000.0) / (Stopwatch.Frequency * _totalIngestCalls);
        source.LogDebug($"Store: {_totalIngestCalls} ingests, avg {avgMicroseconds:F2}µs/candle");
    }
    
    /// <summary>
    /// Reset all period data
    /// </summary>
    public void Reset()
    {
        foreach (PeriodType type in Enum.GetValues<PeriodType>())
        {
            _current[type] = null;
            _previous[type] = null;
        }
        _totalIngestCalls = 0;
        _totalIngestTicks = 0;
        this.LogDebug("Store reset");
    }
}
