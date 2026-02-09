using System.Diagnostics;
using System.Globalization;
using Utils.Common.Logging;

namespace sadnerd.io.ATAS.KeyLevels.DataStore;

/// <summary>
/// Time-based period store with O(1) ingest and session-aware period boundaries.
/// Derives "trading day" from session start times and uses that to determine
/// Weekly, Monthly, Quarterly, and Yearly boundaries.
/// </summary>
public class TimeBasedPeriodStore
{
    private readonly int _timezoneOffset;
    
    // Session tracking
    private DateTime _currentSessionStart = DateTime.MinValue;
    private DateTime _lastTradingDay = DateTime.MinValue;
    
    // Current + Previous per period type
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
    /// Derive the trading day from a session start time.
    /// If session starts at or after noon, the majority of trading falls on the next calendar day.
    /// </summary>
    private static DateTime GetTradingDay(DateTime sessionStart)
    {
        return sessionStart.Hour >= 12
            ? sessionStart.Date.AddDays(1)
            : sessionStart.Date;
    }
    
    /// <summary>
    /// Called when a new session is detected. Triggers period transitions
    /// for Daily and any higher timeframe where the trading day crosses a boundary.
    /// </summary>
    public void SetSessionStart(DateTime sessionStart)
    {
        // Guard against duplicate session starts (IsNewSession can fire for multiple bars)
        if (sessionStart == _currentSessionStart)
        {
            this.LogDebug($"SetSessionStart: duplicate {sessionStart:yyyy-MM-dd HH:mm}, skipping");
            return;
        }
        
        _currentSessionStart = sessionStart;
        var tradingDay = GetTradingDay(sessionStart);
        
        // === Daily: every new session = new daily period ===
        TransitionPeriod(PeriodType.Daily, sessionStart);
        
        // === 4H: reset 4H counting from new session start ===
        // (4H transitions are handled in AddCandleToPeriod based on session start)
        
        if (_lastTradingDay != DateTime.MinValue)
        {
            // === Monday: transition when trading day is Monday, finalize when it's not ===
            if (tradingDay.DayOfWeek == DayOfWeek.Monday && _lastTradingDay.DayOfWeek != DayOfWeek.Monday)
            {
                TransitionPeriod(PeriodType.Monday, sessionStart);
            }
            else if (tradingDay.DayOfWeek != DayOfWeek.Monday)
            {
                // Finalize Monday period when Tuesday (or later) arrives
                var currentMonday = _current[PeriodType.Monday];
                if (currentMonday != null && currentMonday.PeriodEnd == DateTime.MaxValue)
                {
                    currentMonday.PeriodEnd = sessionStart;
                }
            }
            
            // === Weekly: transition when ISO week changes ===
            if (GetIsoWeek(tradingDay) != GetIsoWeek(_lastTradingDay) || tradingDay.Year != _lastTradingDay.Year)
            {
                TransitionPeriod(PeriodType.Weekly, sessionStart);
            }
            
            // === Monthly: transition when month changes ===
            if (tradingDay.Month != _lastTradingDay.Month || tradingDay.Year != _lastTradingDay.Year)
            {
                TransitionPeriod(PeriodType.Monthly, sessionStart);
            }
            
            // === Quarterly: transition when quarter changes ===
            if (GetQuarter(tradingDay) != GetQuarter(_lastTradingDay) || tradingDay.Year != _lastTradingDay.Year)
            {
                TransitionPeriod(PeriodType.Quarterly, sessionStart);
            }
            
            // === Yearly: transition when year changes ===
            if (tradingDay.Year != _lastTradingDay.Year)
            {
                TransitionPeriod(PeriodType.Yearly, sessionStart);
            }
        }
        else
        {
            // First session ever — initialize all periods that don't exist yet
            InitializePeriodIfNull(PeriodType.Monday, tradingDay.DayOfWeek == DayOfWeek.Monday ? sessionStart : DateTime.MinValue);
            InitializePeriodIfNull(PeriodType.Weekly, sessionStart);
            InitializePeriodIfNull(PeriodType.Monthly, sessionStart);
            InitializePeriodIfNull(PeriodType.Quarterly, sessionStart);
            InitializePeriodIfNull(PeriodType.Yearly, sessionStart);
        }
        
        _lastTradingDay = tradingDay;
        
        this.LogDebug($"Session start: {sessionStart:yyyy-MM-dd HH:mm} → trading day: {tradingDay:yyyy-MM-dd} ({tradingDay.DayOfWeek})");
    }
    
    /// <summary>
    /// O(1) ingest per candle — the hot path.
    /// </summary>
    public void AddCandle(DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        _ingestStopwatch.Restart();
        
        // 4H uses session-aligned bounds
        AddCandleTo4H(time, bar, open, high, low, close);
        
        // All other period types: just update current if it exists
        UpdatePeriodIfActive(PeriodType.Daily, time, bar, open, high, low, close);
        UpdatePeriodIfActive(PeriodType.Monday, time, bar, open, high, low, close);
        UpdatePeriodIfActive(PeriodType.Weekly, time, bar, open, high, low, close);
        UpdatePeriodIfActive(PeriodType.Monthly, time, bar, open, high, low, close);
        UpdatePeriodIfActive(PeriodType.Quarterly, time, bar, open, high, low, close);
        UpdatePeriodIfActive(PeriodType.Yearly, time, bar, open, high, low, close);
        
        _ingestStopwatch.Stop();
        _totalIngestTicks += _ingestStopwatch.ElapsedTicks;
        _totalIngestCalls++;
    }
    
    /// <summary>
    /// Transition a period: current becomes previous, create new empty current.
    /// The period start is set to the provided session start time.
    /// </summary>
    private void TransitionPeriod(PeriodType type, DateTime periodStart)
    {
        var current = _current[type];
        if (current != null && current.IsInitialized)
        {
            // Finalize the period end if it wasn't already set (e.g., Monday is finalized by Tuesday)
            if (current.PeriodEnd == DateTime.MaxValue)
            {
                current.PeriodEnd = periodStart;
            }
            _previous[type] = current;
        }
        
        // Create new current with session-start as the period boundary
        _current[type] = new PeriodData(periodStart, DateTime.MaxValue);
    }
    
    /// <summary>
    /// Initialize a period only if it doesn't exist yet.
    /// Used for first-ever session to bootstrap all period types.
    /// </summary>
    private void InitializePeriodIfNull(PeriodType type, DateTime periodStart)
    {
        if (_current[type] == null && periodStart != DateTime.MinValue)
        {
            _current[type] = new PeriodData(periodStart, DateTime.MaxValue);
        }
    }
    
    /// <summary>
    /// Update an existing current period with new candle data.
    /// If the period hasn't been initialized with data yet, initializes it.
    /// </summary>
    private void UpdatePeriodIfActive(PeriodType type, DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        var current = _current[type];
        if (current == null)
            return;
        
        // Don't update periods past their end time (e.g., Monday after Tuesday starts)
        if (current.PeriodEnd != DateTime.MaxValue && time >= current.PeriodEnd)
            return;
        
        if (!current.IsInitialized)
        {
            current.Initialize(time, bar, open, high, low, close);
        }
        else
        {
            current.Update(time, bar, high, low, close);
        }
    }
    
    /// <summary>
    /// 4H periods subdivide the current session into 4-hour blocks.
    /// Transitions happen within AddCandle, not in SetSessionStart.
    /// </summary>
    private void AddCandleTo4H(DateTime time, int bar, decimal open, decimal high, decimal low, decimal close)
    {
        if (_currentSessionStart == DateTime.MinValue)
            return;
        
        var hoursSinceSession = (time - _currentSessionStart).TotalHours;
        if (hoursSinceSession < 0) return; // candle before session start
        
        var periodIndex = (int)(hoursSinceSession / 4);
        var periodStart = _currentSessionStart.AddHours(periodIndex * 4);
        var periodEnd = periodStart.AddHours(4);
        
        var current = _current[PeriodType.FourHour];
        
        // Check if we've moved to a new 4H block
        if (current == null || periodStart != current.PeriodStart)
        {
            if (current != null && current.IsInitialized)
            {
                _previous[PeriodType.FourHour] = current;
            }
            
            current = new PeriodData(periodStart, periodEnd);
            current.Initialize(time, bar, open, high, low, close);
            _current[PeriodType.FourHour] = current;
        }
        else
        {
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
    
    private static int GetIsoWeek(DateTime date)
    {
        return ISOWeek.GetWeekOfYear(date);
    }
    
    private static int GetQuarter(DateTime date)
    {
        return (date.Month - 1) / 3 + 1;
    }
    
    /// <summary>
    /// Log diagnostic summary
    /// </summary>
    public void LogDiagnostics(object source)
    {
        if (_totalIngestCalls == 0) return;
        
        var avgMicroseconds = (_totalIngestTicks * 1_000_000.0) / (Stopwatch.Frequency * _totalIngestCalls);
        source.LogDebug($"Store: {_totalIngestCalls} ingests, avg {avgMicroseconds:F2}µs/candle, session={_currentSessionStart:HH:mm}, tradingDay={_lastTradingDay:yyyy-MM-dd}");
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
        _currentSessionStart = DateTime.MinValue;
        _lastTradingDay = DateTime.MinValue;
        _totalIngestCalls = 0;
        _totalIngestTicks = 0;
        this.LogDebug("Store reset");
    }
}
