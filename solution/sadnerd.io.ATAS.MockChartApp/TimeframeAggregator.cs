using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Aggregates 1-minute candles into higher timeframes.
/// Supports common timeframes: 5m, 15m, 30m, 1H, 4H, Daily.
/// </summary>
public static class TimeframeAggregator
{
    /// <summary>
    /// Aggregate 1-minute candles into the specified timeframe
    /// </summary>
    public static List<IndicatorCandle> Aggregate(List<IndicatorCandle> minuteCandles, Timeframe timeframe)
    {
        if (minuteCandles.Count == 0)
            return new List<IndicatorCandle>();

        int minutesPerBar = GetMinutesPerBar(timeframe);
        var aggregated = new List<IndicatorCandle>();
        
        if (timeframe == Timeframe.Daily)
        {
            return AggregateByDay(minuteCandles);
        }

        // Group by time bucket
        var groups = minuteCandles
            .GroupBy(c => GetBucketTime(c.Time, minutesPerBar))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var candles = group.OrderBy(c => c.Time).ToList();
            var aggregatedCandle = new IndicatorCandle(
                group.Key,
                candles.First().Open,
                candles.Max(c => c.High),
                candles.Min(c => c.Low),
                candles.Last().Close,
                candles.Sum(c => c.Volume)
            );
            aggregated.Add(aggregatedCandle);
        }

        return aggregated;
    }

    /// <summary>
    /// Get the most recent N bars for a timeframe
    /// </summary>
    public static List<IndicatorCandle> GetRecentBars(Timeframe timeframe, int barCount)
    {
        var minuteData = PriceDataStore.MinuteData;
        var aggregated = Aggregate(minuteData, timeframe);
        
        int skip = Math.Max(0, aggregated.Count - barCount);
        return aggregated.Skip(skip).ToList();
    }

    private static List<IndicatorCandle> AggregateByDay(List<IndicatorCandle> minuteCandles)
    {
        var groups = minuteCandles
            .GroupBy(c => c.Time.Date)
            .OrderBy(g => g.Key);

        var aggregated = new List<IndicatorCandle>();
        foreach (var group in groups)
        {
            var candles = group.OrderBy(c => c.Time).ToList();
            var aggregatedCandle = new IndicatorCandle(
                group.Key.AddHours(9).AddMinutes(30), // Session open time
                candles.First().Open,
                candles.Max(c => c.High),
                candles.Min(c => c.Low),
                candles.Last().Close,
                candles.Sum(c => c.Volume)
            );
            aggregated.Add(aggregatedCandle);
        }
        return aggregated;
    }

    private static DateTime GetBucketTime(DateTime time, int minutesPerBar)
    {
        int totalMinutes = (int)(time - time.Date).TotalMinutes;
        int bucketMinutes = (totalMinutes / minutesPerBar) * minutesPerBar;
        return time.Date.AddMinutes(bucketMinutes);
    }

    private static int GetMinutesPerBar(Timeframe timeframe) => timeframe switch
    {
        Timeframe.M1 => 1,
        Timeframe.M5 => 5,
        Timeframe.M15 => 15,
        Timeframe.M30 => 30,
        Timeframe.H1 => 60,
        Timeframe.H4 => 240,
        Timeframe.Daily => 1440,
        _ => 1
    };
}

/// <summary>
/// Supported timeframes for chart display
/// </summary>
public enum Timeframe
{
    M1,
    M5,
    M15,
    M30,
    H1,
    H4,
    Daily
}

/// <summary>
/// Extension methods for Timeframe enum
/// </summary>
public static class TimeframeExtensions
{
    public static string ToDisplayString(this Timeframe tf) => tf switch
    {
        Timeframe.M1 => "1m",
        Timeframe.M5 => "5m",
        Timeframe.M15 => "15m",
        Timeframe.M30 => "30m",
        Timeframe.H1 => "1H",
        Timeframe.H4 => "4H",
        Timeframe.Daily => "D",
        _ => tf.ToString()
    };

    public static Timeframe[] CommonTimeframes => new[]
    {
        Timeframe.M5,
        Timeframe.M15,
        Timeframe.H1,
        Timeframe.H4,
        Timeframe.Daily
    };
}
