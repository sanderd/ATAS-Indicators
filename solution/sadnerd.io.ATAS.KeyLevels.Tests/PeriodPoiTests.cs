using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using sadnerd.io.ATAS.KeyLevels.DataStore;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the simplified PeriodPoi class (bar-based updates).
/// </summary>
public class PeriodPoiTests
{
    private static readonly DateTime DayStart = new(2025, 1, 6, 0, 0, 0);

    [Fact]
    public void Initialize_SetsAllFields()
    {
        var poi = new PeriodPoi { Type = PeriodType.Daily, PeriodStart = DayStart, PeriodEnd = DayStart.AddDays(1) };

        poi.Initialize(DayStart, 5, open: 100, high: 105, low: 95, close: 102);

        Assert.True(poi.IsInitialized);
        Assert.Equal(100m, poi.Open);
        Assert.Equal(105m, poi.High);
        Assert.Equal(95m, poi.Low);
        Assert.Equal(102m, poi.Close);
        Assert.Equal(DayStart, poi.OpenTime);
        Assert.Equal(DayStart, poi.HighTime);
        Assert.Equal(DayStart, poi.LowTime);
        Assert.Equal(DayStart, poi.CloseTime);
        Assert.Equal(5, poi.OpenTimeGranularity);
        Assert.Equal(5, poi.HighTimeGranularity);
        Assert.Equal(5, poi.LowTimeGranularity);
        Assert.Equal(5, poi.CloseTimeGranularity);
        Assert.Equal(DayStart, poi.LatestBarTime);
    }

    [Fact]
    public void UpdateBar_HigherHigh_UpdatesHighAndTimestamp()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 110, low: 97, close: 108);

        Assert.Equal(110m, poi.High);
        Assert.Equal(DayStart.AddHours(1), poi.HighTime);
    }

    [Fact]
    public void UpdateBar_LowerLow_UpdatesLowAndTimestamp()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 103, low: 90, close: 92);

        Assert.Equal(90m, poi.Low);
        Assert.Equal(DayStart.AddHours(1), poi.LowTime);
    }

    [Fact]
    public void UpdateBar_NoNewExtreme_DoesNotChangeHighLow()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 103, low: 97, close: 100);

        Assert.Equal(105m, poi.High);
        Assert.Equal(95m, poi.Low);
        Assert.Equal(DayStart, poi.HighTime); // unchanged
        Assert.Equal(DayStart, poi.LowTime);  // unchanged
    }

    [Fact]
    public void UpdateBar_AlwaysUpdatesClose()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 103, low: 97, close: 101);

        Assert.Equal(101m, poi.Close);
        Assert.Equal(DayStart.AddHours(1), poi.CloseTime);
    }

    [Fact]
    public void UpdateBar_SamePriceMoreGranular_UpdatesTimestamp()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95, candleDuration: 60);

        // Same high price but smaller candle → more granular timestamp
        poi.UpdateBar(DayStart.AddHours(2), 5, high: 105, low: 97, close: 103);

        Assert.Equal(105m, poi.High);
        Assert.Equal(DayStart.AddHours(2), poi.HighTime);
        Assert.Equal(5, poi.HighTimeGranularity);
    }

    [Fact]
    public void UpdateBar_SamePriceLessGranular_DoesNotUpdateTimestamp()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95, candleDuration: 5);

        // Same high price but larger candle → less granular, keep existing
        poi.UpdateBar(DayStart.AddHours(2), 60, high: 105, low: 97, close: 103);

        Assert.Equal(105m, poi.High);
        Assert.Equal(DayStart, poi.HighTime); // unchanged
        Assert.Equal(5, poi.HighTimeGranularity); // unchanged
    }

    [Fact]
    public void UpdateBar_TracksLatestBarTime()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 103, low: 97, close: 100);
        poi.UpdateBar(DayStart.AddHours(3), 5, high: 104, low: 96, close: 101);

        Assert.Equal(DayStart.AddHours(3), poi.LatestBarTime);
    }

    [Fact]
    public void Mid_CalculatesCorrectly()
    {
        var poi = CreateInitializedPoi(high: 110, low: 90);

        Assert.Equal(100m, poi.Mid);
    }

    [Fact]
    public void OpenNeverChanges_AfterInitialize()
    {
        var poi = CreateInitializedPoi(high: 105, low: 95);

        poi.UpdateBar(DayStart.AddHours(1), 5, high: 103, low: 97, close: 101);
        poi.UpdateBar(DayStart.AddHours(2), 5, high: 108, low: 92, close: 106);

        Assert.Equal(100m, poi.Open);
        Assert.Equal(DayStart, poi.OpenTime);
    }

    private PeriodPoi CreateInitializedPoi(decimal high = 105, decimal low = 95, int candleDuration = 5)
    {
        var poi = new PeriodPoi
        {
            Type = PeriodType.Daily,
            PeriodStart = DayStart,
            PeriodEnd = DayStart.AddDays(1)
        };
        poi.Initialize(DayStart, candleDuration, open: 100, high: high, low: low, close: 102);
        return poi;
    }
}
