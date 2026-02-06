using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Generates realistic sample OHLC data for testing the chart and indicators.
/// </summary>
public static class SampleDataGenerator
{
    private static readonly Random _random = new(42); // Fixed seed for reproducibility

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
            var candle = GenerateCandle(currentTime, ref currentPrice, volatility, tickSize);
            candles.Add(candle);

            currentTime = currentTime.AddMinutes(minutesPerBar);
        }

        return candles;
    }

    private static IndicatorCandle GenerateCandle(DateTime time, ref decimal currentPrice, decimal volatility, decimal tickSize)
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

        // Generate volume
        decimal volume = (decimal)(_random.NextDouble() * 1000 + 100);

        currentPrice = close;

        return new IndicatorCandle(time, open, high, low, close, volume);
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

