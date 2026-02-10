using System.Collections.Concurrent;
using System.Globalization;
using sadnerd.io.ATAS.KeyLevels.DataStore;

namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Stores Points of Interest (POI) for a single instrument.
/// Owns period boundary management — accepts individual bars and session starts,
/// determines which periods each bar belongs to, and updates OHLC directly.
/// Thread-safe for concurrent access from multiple indicator instances.
/// </summary>
public class InstrumentDataStore
{
    private readonly object _lock = new();
    
    // Current + Previous per period type
    private readonly Dictionary<PeriodType, PeriodPoi?> _current = new();
    private readonly Dictionary<PeriodType, PeriodPoi?> _previous = new();

    // Session tracking
    private DateTime _currentSessionStart = DateTime.MinValue;
    private DateTime _lastTradingDay = DateTime.MinValue;

    /// <summary>The instrument symbol this store is for.</summary>
    public string Symbol { get; }

    public InstrumentDataStore(string symbol)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        foreach (PeriodType type in Enum.GetValues<PeriodType>())
        {
            _current[type] = null;
            _previous[type] = null;
        }
    }

    /// <summary>
    /// Derive the trading day from a session start time.
    /// If session starts at or after noon, the majority of trading falls on the next calendar day.
    /// This handles DST changes (22:00 vs 23:00) — the session start time itself varies,
    /// but the trading day derivation is always correct.
    /// </summary>
    private static DateTime GetTradingDay(DateTime sessionStart)
    {
        return sessionStart.Hour >= 12
            ? sessionStart.Date.AddDays(1)
            : sessionStart.Date;
    }

    /// <summary>
    /// Called when a new session is detected on any chart.
    /// Triggers period transitions for Daily and higher timeframes.
    /// Session starts naturally vary with DST (22:00 in summer, 23:00 in winter).
    /// </summary>
    public void SetSessionStart(DateTime sessionStart)
    {
        lock (_lock)
        {
            // Guard against duplicate or out-of-order session starts
            if (sessionStart <= _currentSessionStart)
                return;

            _currentSessionStart = sessionStart;
            var tradingDay = GetTradingDay(sessionStart);

            // === Daily: every new session = new daily period ===
            TransitionPeriod(PeriodType.Daily, sessionStart);

            // === 4H: reset from new session (4H transitions are handled in ProcessBar) ===

            if (_lastTradingDay != DateTime.MinValue)
            {
                // === Monday: transition when trading day is Monday ===
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
                // First session ever — initialize all periods
                InitializePeriodIfNull(PeriodType.Monday, tradingDay.DayOfWeek == DayOfWeek.Monday ? sessionStart : DateTime.MinValue);
                InitializePeriodIfNull(PeriodType.Weekly, sessionStart);
                InitializePeriodIfNull(PeriodType.Monthly, sessionStart);
                InitializePeriodIfNull(PeriodType.Quarterly, sessionStart);
                InitializePeriodIfNull(PeriodType.Yearly, sessionStart);
            }

            _lastTradingDay = tradingDay;
        }
    }

    /// <summary>
    /// Process a single bar from any chart. The bar is matched to all
    /// applicable periods and OHLC values are updated if the price is more extreme.
    /// </summary>
    /// <param name="barTime">The bar's timestamp.</param>
    /// <param name="candleDurationMinutes">Candle duration in minutes (for granularity tracking).</param>
    /// <param name="open">Bar open price.</param>
    /// <param name="high">Bar high price.</param>
    /// <param name="low">Bar low price.</param>
    /// <param name="close">Bar close price.</param>
    public void ProcessBar(DateTime barTime, int candleDurationMinutes, decimal open, decimal high, decimal low, decimal close)
    {
        lock (_lock)
        {
            // 4H uses session-aligned bounds
            ProcessBarFor4H(barTime, candleDurationMinutes, open, high, low, close);

            // All other period types: update if bar falls within the period
            UpdatePeriodIfActive(PeriodType.Daily, barTime, candleDurationMinutes, open, high, low, close);
            UpdatePeriodIfActive(PeriodType.Monday, barTime, candleDurationMinutes, open, high, low, close);
            UpdatePeriodIfActive(PeriodType.Weekly, barTime, candleDurationMinutes, open, high, low, close);
            UpdatePeriodIfActive(PeriodType.Monthly, barTime, candleDurationMinutes, open, high, low, close);
            UpdatePeriodIfActive(PeriodType.Quarterly, barTime, candleDurationMinutes, open, high, low, close);
            UpdatePeriodIfActive(PeriodType.Yearly, barTime, candleDurationMinutes, open, high, low, close);
        }
    }

    /// <summary>
    /// Gets the POI for a period.
    /// </summary>
    public PeriodPoi? GetPeriodPoi(PeriodType type, bool isCurrent)
    {
        lock (_lock)
        {
            return isCurrent ? _current[type] : _previous[type];
        }
    }

    /// <summary>
    /// Gets a snapshot of all period data for diagnostics.
    /// </summary>
    public List<(PeriodType Type, bool IsCurrent, PeriodPoi Poi)> GetAllPeriods()
    {
        lock (_lock)
        {
            var result = new List<(PeriodType, bool, PeriodPoi)>();
            foreach (PeriodType type in Enum.GetValues<PeriodType>())
            {
                if (_current[type] is { IsInitialized: true } curr)
                    result.Add((type, true, curr));
                if (_previous[type] is { IsInitialized: true } prev)
                    result.Add((type, false, prev));
            }
            return result;
        }
    }

    /// <summary>Current session start time (for diagnostics).</summary>
    public DateTime CurrentSessionStart
    {
        get { lock (_lock) { return _currentSessionStart; } }
    }

    /// <summary>Clears all stored data.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (PeriodType type in Enum.GetValues<PeriodType>())
            {
                _current[type] = null;
                _previous[type] = null;
            }
            _currentSessionStart = DateTime.MinValue;
            _lastTradingDay = DateTime.MinValue;
        }
    }

    #region Period management internals

    /// <summary>
    /// Transition a period: current becomes previous, create new empty current.
    /// </summary>
    private void TransitionPeriod(PeriodType type, DateTime periodStart)
    {
        var current = _current[type];
        if (current != null && current.IsInitialized)
        {
            if (current.PeriodEnd == DateTime.MaxValue)
            {
                current.PeriodEnd = periodStart;
            }
            _previous[type] = current;
        }

        _current[type] = new PeriodPoi
        {
            Type = type,
            IsCurrent = true,
            PeriodStart = periodStart,
            PeriodEnd = DateTime.MaxValue
        };
    }

    /// <summary>
    /// Initialize a period only if it doesn't exist yet (first session bootstrap).
    /// </summary>
    private void InitializePeriodIfNull(PeriodType type, DateTime periodStart)
    {
        if (_current[type] == null && periodStart != DateTime.MinValue)
        {
            _current[type] = new PeriodPoi
            {
                Type = type,
                IsCurrent = true,
                PeriodStart = periodStart,
                PeriodEnd = DateTime.MaxValue
            };
        }
    }

    /// <summary>
    /// Update a period's POI if the bar falls within the period's time range.
    /// </summary>
    private void UpdatePeriodIfActive(PeriodType type, DateTime barTime, int candleDuration,
        decimal open, decimal high, decimal low, decimal close)
    {
        var poi = _current[type];
        if (poi == null)
            return;

        // Don't update periods past their end time
        if (poi.PeriodEnd != DateTime.MaxValue && barTime >= poi.PeriodEnd)
            return;

        // Bar must be at or after period start
        if (barTime < poi.PeriodStart)
            return;

        if (!poi.IsInitialized)
        {
            poi.Initialize(barTime, candleDuration, open, high, low, close);
        }
        else
        {
            poi.UpdateBar(barTime, candleDuration, high, low, close);
        }
    }

    /// <summary>
    /// 4H periods subdivide the current session into 4-hour blocks.
    /// </summary>
    private void ProcessBarFor4H(DateTime barTime, int candleDuration,
        decimal open, decimal high, decimal low, decimal close)
    {
        if (_currentSessionStart == DateTime.MinValue)
            return;

        var hoursSinceSession = (barTime - _currentSessionStart).TotalHours;
        if (hoursSinceSession < 0) return;

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

            current = new PeriodPoi
            {
                Type = PeriodType.FourHour,
                IsCurrent = true,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
            current.Initialize(barTime, candleDuration, open, high, low, close);
            _current[PeriodType.FourHour] = current;
        }
        else
        {
            current.UpdateBar(barTime, candleDuration, high, low, close);
        }
    }

    #endregion

    private static int GetIsoWeek(DateTime date) => ISOWeek.GetWeekOfYear(date);
    private static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

    public override string ToString() =>
        $"InstrumentDataStore[{Symbol}]: session={_currentSessionStart:HH:mm}";
}
