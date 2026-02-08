using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the InstrumentDataStore class.
/// </summary>
public class InstrumentDataStoreTests
{
    private static readonly DateTime DayStart = new(2026, 2, 8, 0, 0, 0);
    private static readonly DateTime DayEnd = new(2026, 2, 9, 0, 0, 0);

    [Fact]
    public void Constructor_SetsSymbol()
    {
        var store = new InstrumentDataStore("ES");
        Assert.Equal("ES", store.Symbol);
    }

    [Fact]
    public void Constructor_ThrowsOnNullSymbol()
    {
        Assert.Throws<ArgumentNullException>(() => new InstrumentDataStore(null!));
    }

    [Fact]
    public void ContributePeriodData_CreatesNewPoi()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        store.ContributePeriodData(PeriodType.Daily, isCurrent: true, DayStart, DayEnd, range);

        // Assert
        var poi = store.GetPeriodPoi(PeriodType.Daily, isCurrent: true);
        Assert.NotNull(poi);
        Assert.Equal(100, poi.Open);
        Assert.True(poi.HasCompleteCoverage());
    }

    [Fact]
    public void ContributePeriodData_MergesWithExisting()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var range1 = new TimeRange
        {
            Start = DayStart,
            End = DayStart.AddHours(12),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = DayStart.AddHours(12),
            End = DayEnd,
            Open = 105, High = 125, Low = 100, Close = 120
        };

        // Act
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range1);
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range2);

        // Assert
        var poi = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.NotNull(poi);
        Assert.True(poi.HasCompleteCoverage());
        Assert.Equal(125, poi.High);
        Assert.Equal(95, poi.Low);
    }

    [Fact]
    public void GetPeriodPoi_ReturnsNullForUnknown()
    {
        var store = new InstrumentDataStore("ES");
        
        var poi = store.GetPeriodPoi(PeriodType.Daily, true);
        
        Assert.Null(poi);
    }

    [Fact]
    public void HasCompleteCoverage_DelegatesToPoi()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var partialRange = new TimeRange
        {
            Start = DayStart.AddHours(6),
            End = DayEnd,
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var fullRange = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, partialRange);
        store.ContributePeriodData(PeriodType.Weekly, true, DayStart, DayEnd, fullRange);

        // Assert
        Assert.False(store.HasCompleteCoverage(PeriodType.Daily, true));
        Assert.True(store.HasCompleteCoverage(PeriodType.Weekly, true));
        Assert.False(store.HasCompleteCoverage(PeriodType.Monthly, true)); // Not contributed
    }

    [Fact]
    public void ContributePeriodData_HandlesCurrentAndPreviousSeparately()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var currentRange = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };
        var previousRange = new TimeRange
        {
            Start = DayStart.AddDays(-1),
            End = DayStart,
            Open = 95, High = 115, Low = 85, Close = 100
        };

        // Act
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, currentRange);
        store.ContributePeriodData(PeriodType.Daily, false, DayStart.AddDays(-1), DayStart, previousRange);

        // Assert
        var currentPoi = store.GetPeriodPoi(PeriodType.Daily, true);
        var previousPoi = store.GetPeriodPoi(PeriodType.Daily, false);
        
        Assert.NotNull(currentPoi);
        Assert.NotNull(previousPoi);
        Assert.Equal(120, currentPoi.High);
        Assert.Equal(115, previousPoi.High);
    }

    [Fact]
    public void ContributePeriodData_ResetsPoi_WhenPeriodBoundariesChange()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var range1 = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };
        var range2 = new TimeRange
        {
            Start = DayEnd,
            End = DayEnd.AddDays(1),
            Open = 110, High = 130, Low = 105, Close = 125
        };

        // Act - first contribution
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range1);
        
        // Act - new period (next day)
        store.ContributePeriodData(PeriodType.Daily, true, DayEnd, DayEnd.AddDays(1), range2);

        // Assert - POI should be for the new period
        var poi = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.NotNull(poi);
        Assert.Equal(DayEnd, poi.PeriodStart);
        Assert.Equal(110, poi.Open);
    }

    [Fact]
    public void GetAllPeriods_ReturnsContributedPeriods()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range);
        store.ContributePeriodData(PeriodType.Weekly, true, DayStart, DayEnd, range);
        store.ContributePeriodData(PeriodType.Daily, false, DayStart.AddDays(-1), DayStart, range);

        // Assert
        var periods = store.GetAllPeriods();
        Assert.Equal(3, periods.Count);
        Assert.Contains(periods, p => p.Type == PeriodType.Daily && p.IsCurrent);
        Assert.Contains(periods, p => p.Type == PeriodType.Daily && !p.IsCurrent);
        Assert.Contains(periods, p => p.Type == PeriodType.Weekly && p.IsCurrent);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };
        store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range);

        // Act
        store.Clear();

        // Assert
        Assert.Null(store.GetPeriodPoi(PeriodType.Daily, true));
        Assert.Empty(store.GetAllPeriods());
    }

    [Fact]
    public void ConcurrentContributions_AreThreadSafe()
    {
        // Arrange
        var store = new InstrumentDataStore("ES");
        var tasks = new List<Task>();

        // Act - simulate 10 concurrent indicator instances contributing
        for (int i = 0; i < 10; i++)
        {
            int offset = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var range = new TimeRange
                    {
                        Start = DayStart.AddHours(offset),
                        End = DayStart.AddHours(offset + 1),
                        Open = 100 + offset,
                        High = 110 + offset,
                        Low = 90 + offset,
                        Close = 105 + offset,
                        SourceId = $"Thread{offset}"
                    };
                    store.ContributePeriodData(PeriodType.Daily, true, DayStart, DayEnd, range);
                }
            }));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert - should not throw, and should have valid data
        var poi = store.GetPeriodPoi(PeriodType.Daily, true);
        Assert.NotNull(poi);
        Assert.True(poi.CoveredRanges.Count >= 1);
    }
}
