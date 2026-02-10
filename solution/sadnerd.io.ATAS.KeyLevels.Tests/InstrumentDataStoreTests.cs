using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using sadnerd.io.ATAS.KeyLevels.DataStore;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the bar-based InstrumentDataStore.
/// </summary>
public class InstrumentDataStoreTests
{
    private static readonly DateTime SessionStart = new(2025, 1, 6, 23, 0, 0); // Sunday 23:00

    [Fact]
    public void SetSessionStart_CreatesDailyPeriod()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);

        var daily = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.NotNull(daily);
        Assert.Equal(SessionStart, daily!.PeriodStart);
    }

    [Fact]
    public void ProcessBar_InitializesPoi()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);

        var daily = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.NotNull(daily);
        Assert.True(daily!.IsInitialized);
        Assert.Equal(100m, daily.Open);
        Assert.Equal(105m, daily.High);
        Assert.Equal(95m, daily.Low);
        Assert.Equal(102m, daily.Close);
    }

    [Fact]
    public void ProcessBar_UpdatesHighLow()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);
        store.ProcessBar(SessionStart.AddMinutes(5), 5, 102, 110, 97, 108);

        var daily = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.Equal(110m, daily!.High);
        Assert.Equal(95m, daily.Low); // Original low is still lower
    }

    [Fact]
    public void SetSessionStart_TransitionsDailyPeriod()
    {
        var store = new InstrumentDataStore("ES");
        var day1 = SessionStart;
        var day2 = SessionStart.AddDays(1);

        store.SetSessionStart(day1);
        store.ProcessBar(day1, 5, 100, 105, 95, 102);

        store.SetSessionStart(day2);
        store.ProcessBar(day2, 5, 103, 108, 99, 106);

        var prevDaily = store.GetPeriodPoi(PeriodType.Daily, false);
        var currDaily = store.GetPeriodPoi(PeriodType.Daily, true);

        Assert.NotNull(prevDaily);
        Assert.NotNull(currDaily);
        Assert.Equal(105m, prevDaily!.High);
        Assert.Equal(108m, currDaily!.High);
    }

    [Fact]
    public void SetSessionStart_TransitionsWeeklyOnIsoWeekChange()
    {
        var store = new InstrumentDataStore("ES");

        // Monday Jan 6 session (trading day Mon)
        var monday = new DateTime(2025, 1, 5, 23, 0, 0); // Sun 23:00 → trading day Mon Jan 6
        store.SetSessionStart(monday);
        store.ProcessBar(monday, 5, 100, 105, 95, 102);

        // Skip ahead to next Monday (Jan 13)
        var nextMonday = new DateTime(2025, 1, 12, 23, 0, 0); // Sun 23:00 → trading day Mon Jan 13
        // Need intermediate sessions to trigger week change
        for (int i = 1; i <= 5; i++)
        {
            var sessionStart = monday.AddDays(i);
            store.SetSessionStart(sessionStart);
            store.ProcessBar(sessionStart, 5, 100 + i, 105 + i, 95 - i, 102);
        }
        store.SetSessionStart(nextMonday);
        store.ProcessBar(nextMonday, 5, 200, 210, 190, 205);

        var prevWeek = store.GetPeriodPoi(PeriodType.Weekly, false);
        var currWeek = store.GetPeriodPoi(PeriodType.Weekly, true);

        Assert.NotNull(prevWeek);
        Assert.NotNull(currWeek);
        Assert.True(prevWeek!.IsInitialized);
        Assert.True(currWeek!.IsInitialized);
    }

    [Fact]
    public void FourHour_TransitionsCorrectly()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);

        // Bars within first 4H block
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);
        store.ProcessBar(SessionStart.AddHours(1), 5, 102, 107, 97, 104);

        // Bar in second 4H block
        store.ProcessBar(SessionStart.AddHours(4), 5, 104, 110, 99, 108);

        var prev4h = store.GetPeriodPoi(PeriodType.FourHour, false);
        var curr4h = store.GetPeriodPoi(PeriodType.FourHour, true);

        Assert.NotNull(prev4h);
        Assert.NotNull(curr4h);
        Assert.Equal(107m, prev4h!.High);
        Assert.Equal(110m, curr4h!.High);
    }

    [Fact]
    public void GetAllPeriods_ReturnsOnlyInitialized()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);

        var periods = store.GetAllPeriods();

        Assert.True(periods.Count > 0);
        Assert.All(periods, p => Assert.True(p.Poi.IsInitialized));
    }

    [Fact]
    public void Clear_ResetsAllData()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);

        store.Clear();

        var daily = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.Null(daily);
        Assert.Equal(DateTime.MinValue, store.CurrentSessionStart);
    }

    [Fact]
    public void DuplicateSessionStart_IsIgnored()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);
        store.ProcessBar(SessionStart, 5, 100, 105, 95, 102);

        // Same session start again
        store.SetSessionStart(SessionStart);

        // Should still have the same daily — no transition
        var prevDaily = store.GetPeriodPoi(PeriodType.Daily, false);
        Assert.Null(prevDaily); // No previous = no spurious transition
    }

    [Fact]
    public void ProcessBar_GranularityPreserved()
    {
        var store = new InstrumentDataStore("ES");
        store.SetSessionStart(SessionStart);

        // 60-min candle with high of 105
        store.ProcessBar(SessionStart, 60, 100, 105, 95, 102);

        // 5-min candle with same high price — should update timestamp for better granularity
        store.ProcessBar(SessionStart.AddMinutes(30), 5, 103, 105, 97, 104);

        var daily = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.Equal(105m, daily!.High);
        Assert.Equal(SessionStart.AddMinutes(30), daily.HighTime);
        Assert.Equal(5, daily.HighTimeGranularity);
    }
}
