using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Central store for minute-level price data.
/// Provides 2 years of 1-minute OHLCV data for multi-chart timeframe aggregation.
/// </summary>
public static class PriceDataStore
{
    private static List<IndicatorCandle>? _minuteData;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Get the full 1-minute dataset (lazy initialization)
    /// </summary>
    public static List<IndicatorCandle> MinuteData
    {
        get
        {
            if (_minuteData == null)
            {
                lock (_lock)
                {
                    _minuteData ??= GenerateTwoYearsOfMinuteData();
                }
            }
            return _minuteData;
        }
    }
    
    /// <summary>
    /// Get candles for a specific date range
    /// </summary>
    public static List<IndicatorCandle> GetRange(DateTime start, DateTime end)
    {
        return MinuteData
            .Where(c => c.Time >= start && c.Time <= end)
            .ToList();
    }
    
    /// <summary>
    /// Get the most recent N days of minute data
    /// </summary>
    public static List<IndicatorCandle> GetLastDays(int days)
    {
        if (MinuteData.Count == 0)
            return new List<IndicatorCandle>();
            
        var lastTime = MinuteData[^1].Time;
        var startTime = lastTime.AddDays(-days);
        return GetRange(startTime, lastTime);
    }
    
    /// <summary>
    /// Reset the data store (useful for testing)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _minuteData = null;
        }
    }
    
    /// <summary>
    /// Generate 2 years of 1-minute data (~500k candles for trading hours only)
    /// </summary>
    private static List<IndicatorCandle> GenerateTwoYearsOfMinuteData()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var candles = new List<IndicatorCandle>();
        
        // Start 2 years ago from a Monday
        var startDate = new DateTime(2024, 1, 8, 9, 30, 0); // Monday, January 8, 2024
        var endDate = startDate.AddYears(2);
        
        decimal basePrice = 4500m;
        decimal currentPrice = basePrice;
        decimal tickSize = 0.25m;
        
        // Session hours (9:30 AM to 4:00 PM EST = 6.5 hours = 390 minutes)
        int sessionStartHour = 9;
        int sessionStartMinute = 30;
        int sessionEndHour = 16;
        
        // Tracking for volume generation
        decimal recentHigh = 0;
        decimal recentLow = decimal.MaxValue;
        int trendBars = 0;
        int trendDirection = 0;
        
        DateTime currentTime = startDate;
        
        while (currentTime < endDate)
        {
            // Skip weekends
            if (currentTime.DayOfWeek == DayOfWeek.Saturday || currentTime.DayOfWeek == DayOfWeek.Sunday)
            {
                currentTime = currentTime.AddDays(1).Date.AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
                recentHigh = 0;
                recentLow = decimal.MaxValue;
                continue;
            }
            
            var sessionStart = currentTime.Date.AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
            var sessionEnd = currentTime.Date.AddHours(sessionEndHour);
            
            if (currentTime < sessionStart)
            {
                currentTime = sessionStart;
                continue;
            }
            
            if (currentTime >= sessionEnd)
            {
                currentTime = currentTime.Date.AddDays(1).AddHours(sessionStartHour).AddMinutes(sessionStartMinute);
                recentHigh = 0;
                recentLow = decimal.MaxValue;
                continue;
            }
            
            // Volatility
            decimal baseVolatility = 0.0003m; // Smaller for 1-min bars
            int hourOfDay = currentTime.Hour;
            decimal volatilityMultiplier = hourOfDay switch
            {
                9 or 10 => 1.5m,
                >= 14 and < 16 => 1.3m,
                _ => 1.0m
            };
            decimal volatility = baseVolatility * volatilityMultiplier;
            
            // Generate price movement
            double randomValue = random.NextDouble() - 0.5;
            decimal movement = (decimal)randomValue * currentPrice * volatility * 2m;
            movement = Math.Round(movement / tickSize) * tickSize;
            
            decimal open = currentPrice;
            decimal close = Math.Clamp(open + movement, 3500m, 6500m);
            
            // High/Low
            decimal rangeBase = currentPrice * volatility;
            decimal extraRange = (decimal)random.NextDouble() * rangeBase;
            
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
            
            high = Math.Round(high / tickSize) * tickSize;
            low = Math.Round(low / tickSize) * tickSize;
            close = Math.Round(close / tickSize) * tickSize;
            high = Math.Max(high, Math.Max(open, close));
            low = Math.Min(low, Math.Min(open, close));
            
            // Volume generation (simplified from SampleDataGenerator)
            double baseRandom = random.NextDouble();
            decimal baseVolume = 200m + (decimal)(baseRandom * baseRandom * 500);
            decimal volumeMultiplier = 1.0m;
            
            if (hourOfDay == 9) volumeMultiplier *= 1.8m;
            else if (hourOfDay == 10) volumeMultiplier *= 1.4m;
            else if (hourOfDay >= 11 && hourOfDay <= 13) volumeMultiplier *= 0.7m;
            else if (hourOfDay >= 15) volumeMultiplier *= 1.5m;
            
            bool isNewHigh = high > recentHigh && recentHigh > 0;
            bool isNewLow = low < recentLow && recentLow < decimal.MaxValue;
            if ((isNewHigh || isNewLow) && random.NextDouble() > 0.7)
                volumeMultiplier *= 2.0m;
            
            decimal volume = Math.Round(baseVolume * volumeMultiplier);
            
            candles.Add(new IndicatorCandle(currentTime, open, high, low, close, volume));
            
            // Update tracking
            if (high > recentHigh || recentHigh == 0) recentHigh = high;
            if (low < recentLow) recentLow = low;
            recentHigh -= 0.25m;
            recentLow += 0.25m;
            
            int dir = close > open ? 1 : -1;
            if (dir == trendDirection) trendBars += dir;
            else { trendBars = dir; trendDirection = dir; }
            
            currentPrice = close;
            currentTime = currentTime.AddMinutes(1);
        }
        
        return candles;
    }
}
