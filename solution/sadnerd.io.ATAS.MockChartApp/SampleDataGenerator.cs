using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Generates realistic sample OHLC data for testing the chart and indicators.
/// Includes realistic volume patterns for PVSRA indicator.
/// </summary>
public static class SampleDataGenerator
{
    private static readonly Random _random = new(42); // Fixed seed for reproducibility

    // Track recent data for realistic volume generation
    private static decimal _recentHigh;
    private static decimal _recentLow;
    private static decimal _avgVolume;
    private static int _trendBars;
    private static int _trendDirection; // 1 = up, -1 = down, 0 = neutral

    /// <summary>
    /// Generate sample candle data spanning multiple days, weeks, and quarters
    /// to exercise all KeyLevels functionality.
    /// </summary>
    /// <param name="daysOfData">Number of days of data to generate</param>
    /// <param name="minutesPerBar">Timeframe in minutes</param>
    /// <returns>List of candles</returns>
    public static List<IndicatorCandle> GenerateData(int daysOfData = 30, int minutesPerBar = 5)
    {
        var candles = new List<IndicatorCandle>();
        
        // Reset state
        _recentHigh = 0;
        _recentLow = decimal.MaxValue;
        _avgVolume = 500m;
        _trendBars = 0;
        _trendDirection = 0;
        
        // Start from a Monday to ensure we have Monday data
        // Use a date that's in the middle of a quarter for quarterly data
        var startDate = new DateTime(2025, 11, 3, 9, 30, 0); // Monday, November 3, 2025, market open
        
        decimal basePrice = 5000m; // Starting price (like ES futures)
        decimal currentPrice = basePrice;
        decimal tickSize = 0.25m;

        // Session hours (9:30 AM to 4:00 PM EST)
        int sessionStartHour = 9;
        int sessionStartMinute = 30;
        int sessionEndHour = 16;
        int sessionEndMinute = 0;

        DateTime currentTime = startDate;
        DateTime endDate = startDate.AddDays(daysOfData);

        while (currentTime < endDate)
        {
            // Skip weekends
            if (currentTime.DayOfWeek == DayOfWeek.Saturday || currentTime.DayOfWeek == DayOfWeek.Sunday)
            {
                currentTime = currentTime.AddDays(1).Date.AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
                // Reset session tracking
                _recentHigh = 0;
                _recentLow = decimal.MaxValue;
                continue;
            }

            // Check if within session hours
            var sessionStart = currentTime.Date.AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
            var sessionEnd = currentTime.Date.AddHours(sessionEndHour).AddMinutes(sessionEndMinute);

            if (currentTime < sessionStart)
            {
                currentTime = sessionStart;
                continue;
            }

            if (currentTime >= sessionEnd)
            {
                // Move to next day
                currentTime = currentTime.Date.AddDays(1).AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
                // Reset session tracking
                _recentHigh = 0;
                _recentLow = decimal.MaxValue;
                continue;
            }

            // Calculate volatility for this bar (does not accumulate)
            decimal baseVolatility = 0.0005m; // 0.05% base volatility per bar
            
            // Add session-specific volatility
            var hourOfDay = currentTime.Hour;
            decimal volatilityMultiplier = 1.0m;
            if (hourOfDay == 9 || hourOfDay == 10) // Opening volatility
            {
                volatilityMultiplier = 1.5m;
            }
            else if (hourOfDay >= 14 && hourOfDay < 16) // Closing volatility
            {
                volatilityMultiplier = 1.3m;
            }

            decimal volatility = baseVolatility * volatilityMultiplier;

            // Generate candle
            var candle = GenerateCandle(currentTime, ref currentPrice, volatility, tickSize, hourOfDay);
            candles.Add(candle);

            currentTime = currentTime.AddMinutes(minutesPerBar);
        }

        return candles;
    }

    private static IndicatorCandle GenerateCandle(DateTime time, ref decimal currentPrice, decimal volatility, decimal tickSize, int hourOfDay)
    {
        // Generate random price movement using simple random walk
        double randomValue = _random.NextDouble() - 0.5; // Range: -0.5 to 0.5
        decimal movement = (decimal)randomValue * currentPrice * volatility * 2m;
        
        // Round to tick size
        movement = Math.Round(movement / tickSize) * tickSize;

        decimal open = currentPrice;
        decimal close = open + movement;
        
        // Keep price in reasonable bounds (prevent runaway prices)
        close = Math.Max(4000m, Math.Min(6000m, close));
        
        // Generate high and low based on range
        decimal rangeBase = currentPrice * volatility;
        decimal extraRange = (decimal)_random.NextDouble() * rangeBase;
        
        decimal high, low;
        if (close >= open)
        {
            high = Math.Max(open, close) + extraRange * 0.5m;
            low = Math.Min(open, close) - extraRange * 0.3m;
        }
        else
        {
            high = Math.Max(open, close) + extraRange * 0.3m;
            low = Math.Min(open, close) - extraRange * 0.5m;
        }

        // Round to tick size
        high = Math.Round(high / tickSize) * tickSize;
        low = Math.Round(low / tickSize) * tickSize;
        close = Math.Round(close / tickSize) * tickSize;

        // Ensure high >= open,close and low <= open,close
        high = Math.Max(high, Math.Max(open, close));
        low = Math.Min(low, Math.Min(open, close));

        // Generate realistic volume
        decimal volume = GenerateVolume(open, high, low, close, hourOfDay);

        // Update tracking for next candle
        UpdateTracking(high, low, close > open ? 1 : -1);

        currentPrice = close;

        return new IndicatorCandle(time, open, high, low, close, volume);
    }

    private static decimal GenerateVolume(decimal open, decimal high, decimal low, decimal close, int hourOfDay)
    {
        // Base volume using lognormal-ish distribution
        double baseRandom = _random.NextDouble();
        decimal baseVolume = 300m + (decimal)(baseRandom * baseRandom * 700); // Skewed distribution
        
        decimal multiplier = 1.0m;
        
        // Time-of-day patterns
        if (hourOfDay == 9) // Market open
            multiplier *= 1.8m;
        else if (hourOfDay == 10)
            multiplier *= 1.4m;
        else if (hourOfDay >= 11 && hourOfDay <= 13) // Lunch lull
            multiplier *= 0.7m;
        else if (hourOfDay >= 15) // Closing hour
            multiplier *= 1.5m;
        
        // Spread (range) correlation - big moves = more volume
        decimal spread = Math.Abs(close - open);
        decimal range = high - low;
        if (range > 0)
        {
            decimal spreadRatio = spread / range;
            if (spreadRatio > 0.7m) // Strong directional bar
                multiplier *= 1.3m;
        }
        
        // Volume spikes at extremes (stopping volume, reversals)
        bool isNewHigh = high > _recentHigh && _recentHigh > 0;
        bool isNewLow = low < _recentLow && _recentLow < decimal.MaxValue;
        
        if (isNewHigh || isNewLow)
        {
            // Potential reversal/stopping volume
            if (_random.NextDouble() > 0.6) // 40% chance of spike at extreme
            {
                multiplier *= 2.0m + (decimal)(_random.NextDouble() * 1.5); // 2x-3.5x volume
            }
        }
        
        // Stop hunt pattern: break of recent high/low with rejection (wick)
        decimal upperWick = high - Math.Max(open, close);
        decimal lowerWick = Math.Min(open, close) - low;
        bool hasRejectionWick = (isNewHigh && upperWick > spread) || (isNewLow && lowerWick > spread);
        
        if (hasRejectionWick && _random.NextDouble() > 0.5)
        {
            multiplier *= 2.5m + (decimal)(_random.NextDouble()); // Stop hunt = high volume
        }
        
        // Trend continuation: extended trend gets climax volume eventually
        if (Math.Abs(_trendBars) > 5)
        {
            if (_random.NextDouble() > 0.85) // Occasional climax
            {
                multiplier *= 2.2m;
            }
        }
        
        // Update average volume (for PVSRA to have meaningful comparisons)
        decimal volume = baseVolume * multiplier;
        _avgVolume = _avgVolume * 0.9m + volume * 0.1m; // Smooth average
        
        return Math.Round(volume);
    }
    
    private static void UpdateTracking(decimal high, decimal low, int direction)
    {
        // Track recent high/low for extreme detection
        if (high > _recentHigh || _recentHigh == 0)
            _recentHigh = high;
        if (low < _recentLow)
            _recentLow = low;
        
        // Decay recent extremes slowly (so they're "recent" not "all-time")
        _recentHigh -= 0.5m;
        _recentLow += 0.5m;
        
        // Track trend
        if (direction == _trendDirection)
        {
            _trendBars += direction;
        }
        else
        {
            _trendBars = direction;
            _trendDirection = direction;
        }
    }

    /// <summary>
    /// Generate a smaller dataset for quick testing
    /// </summary>
    public static List<IndicatorCandle> GenerateQuickTestData()
    {
        return GenerateData(daysOfData: 5, minutesPerBar: 1);
    }

    /// <summary>
    /// Generate a large dataset spanning multiple quarters
    /// </summary>
    public static List<IndicatorCandle> GenerateLargeDataset()
    {
        return GenerateData(daysOfData: 120, minutesPerBar: 5);
    }
}
