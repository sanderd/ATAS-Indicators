using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using sadnerd.io.ATAS.KeyLevels.DataStore;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the PeriodPoi class.
/// </summary>
public class PeriodPoiTests
{
    private static readonly DateTime DayStart = new(2026, 2, 8, 0, 0, 0);
    private static readonly DateTime DayEnd = new(2026, 2, 9, 0, 0, 0);

    private PeriodPoi CreateDailyPoi(bool isCurrent = true) => new()
    {
        Type = PeriodType.Daily,
        IsCurrent = isCurrent,
        PeriodStart = DayStart,
        PeriodEnd = DayEnd
    };

    [Fact]
    public void AddContribution_SingleRange_SetsOhlc()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        poi.AddContribution(range);

        // Assert
        Assert.Equal(100, poi.Open);
        Assert.Equal(120, poi.High);
        Assert.Equal(90, poi.Low);
        Assert.Equal(110, poi.Close);
        Assert.Equal(105, poi.Mid);
        Assert.Single(poi.CoveredRanges);
    }

    [Fact]
    public void AddContribution_FullPeriod_HasCompleteCoverage()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        poi.AddContribution(range);

        // Assert
        Assert.True(poi.HasCompleteCoverage());
    }

    [Fact]
    public void AddContribution_PartialPeriod_HasIncompleteCoverage()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range = new TimeRange
        {
            Start = DayStart.AddHours(6), // Starts at 6am, not midnight
            End = DayEnd,
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        poi.AddContribution(range);

        // Assert
        Assert.False(poi.HasCompleteCoverage());
    }

    [Fact]
    public void AddContribution_TwoContiguousRanges_MergesAndHasCompleteCoverage()
    {
        // Arrange
        var poi = CreateDailyPoi();
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
        poi.AddContribution(range1);
        poi.AddContribution(range2);

        // Assert
        Assert.True(poi.HasCompleteCoverage());
        Assert.Single(poi.CoveredRanges); // Merged into one
        Assert.Equal(100, poi.Open);
        Assert.Equal(125, poi.High);
        Assert.Equal(95, poi.Low);
        Assert.Equal(120, poi.Close);
    }

    [Fact]
    public void AddContribution_GapBetweenRanges_GetGapsReturnsGap()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range1 = new TimeRange
        {
            Start = DayStart,
            End = DayStart.AddHours(6),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = DayStart.AddHours(12), // Gap from 6am to 12pm
            End = DayEnd,
            Open = 105, High = 120, Low = 100, Close = 115
        };

        // Act
        poi.AddContribution(range1);
        poi.AddContribution(range2);

        // Assert
        Assert.False(poi.HasCompleteCoverage());
        var gaps = poi.GetGaps().ToList();
        Assert.Single(gaps);
        Assert.Equal(DayStart.AddHours(6), gaps[0].Start);
        Assert.Equal(DayStart.AddHours(12), gaps[0].End);
    }

    [Fact]
    public void HasCoverageAt_ReturnsTrueForCoveredTime()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range = new TimeRange
        {
            Start = DayStart,
            End = DayStart.AddHours(12),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        poi.AddContribution(range);

        // Act & Assert
        Assert.True(poi.HasCoverageAt(DayStart.AddHours(6)));
        Assert.False(poi.HasCoverageAt(DayStart.AddHours(18)));
    }

    [Fact]
    public void AddContribution_ClipsRangeToperiodBoundaries()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range = new TimeRange
        {
            Start = DayStart.AddHours(-2), // Before period start
            End = DayEnd.AddHours(2),       // After period end
            Open = 100, High = 120, Low = 90, Close = 110
        };

        // Act
        poi.AddContribution(range);

        // Assert
        Assert.Single(poi.CoveredRanges);
        Assert.Equal(DayStart, poi.CoveredRanges[0].Start);
        Assert.Equal(DayEnd, poi.CoveredRanges[0].End);
    }

    [Fact]
    public void AddContribution_MultipleOverlapping_MergesCorrectly()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var ranges = new[]
        {
            new TimeRange { Start = DayStart, End = DayStart.AddHours(8), Open = 100, High = 105, Low = 98, Close = 102 },
            new TimeRange { Start = DayStart.AddHours(4), End = DayStart.AddHours(12), Open = 102, High = 110, Low = 100, Close = 108 },
            new TimeRange { Start = DayStart.AddHours(8), End = DayStart.AddHours(16), Open = 108, High = 115, Low = 105, Close = 112 },
            new TimeRange { Start = DayStart.AddHours(12), End = DayEnd, Open = 112, High = 120, Low = 108, Close = 118 }
        };

        // Act
        foreach (var range in ranges)
        {
            poi.AddContribution(range);
        }

        // Assert
        Assert.True(poi.HasCompleteCoverage());
        Assert.Single(poi.CoveredRanges);
        Assert.Equal(100, poi.Open);
        Assert.Equal(120, poi.High);
        Assert.Equal(98, poi.Low);
        Assert.Equal(118, poi.Close);
    }

    [Fact]
    public void TotalGapDuration_CalculatesCorrectly()
    {
        // Arrange
        var poi = CreateDailyPoi();
        var range1 = new TimeRange
        {
            Start = DayStart,
            End = DayStart.AddHours(6),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = DayStart.AddHours(12),
            End = DayStart.AddHours(18),
            Open = 105, High = 120, Low = 100, Close = 115
        };

        // Act
        poi.AddContribution(range1);
        poi.AddContribution(range2);

        // Assert: Gaps are 6am-12pm (6h) and 6pm-midnight (6h) = 12h total
        Assert.Equal(TimeSpan.FromHours(12), poi.TotalGapDuration);
    }

    [Fact]
    public void GetGaps_EmptyPoi_ReturnsEntirePeriod()
    {
        // Arrange
        var poi = CreateDailyPoi();

        // Act
        var gaps = poi.GetGaps().ToList();

        // Assert
        Assert.Single(gaps);
        Assert.Equal(DayStart, gaps[0].Start);
        Assert.Equal(DayEnd, gaps[0].End);
    }
}
