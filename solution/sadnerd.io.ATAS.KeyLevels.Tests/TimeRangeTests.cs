using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the TimeRange class.
/// </summary>
public class TimeRangeTests
{
    private static readonly DateTime BaseTime = new(2026, 2, 8, 9, 0, 0);

    [Fact]
    public void IsContiguousWith_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var range1 = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = BaseTime.AddHours(1),
            End = BaseTime.AddHours(2),
            Open = 105, High = 115, Low = 100, Close = 110
        };

        // Act & Assert
        Assert.True(range1.IsContiguousWith(range2));
        Assert.True(range2.IsContiguousWith(range1));
    }

    [Fact]
    public void IsContiguousWith_SmallGap_ReturnsTrue()
    {
        // Arrange: 30 second gap (less than 1 minute tolerance)
        var range1 = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = BaseTime.AddHours(1).AddSeconds(30),
            End = BaseTime.AddHours(2),
            Open = 105, High = 115, Low = 100, Close = 110
        };

        // Act & Assert
        Assert.True(range1.IsContiguousWith(range2));
    }

    [Fact]
    public void IsContiguousWith_LargeGap_ReturnsFalse()
    {
        // Arrange: 2 minute gap (more than 1 minute tolerance)
        var range1 = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = BaseTime.AddHours(1).AddMinutes(2),
            End = BaseTime.AddHours(2),
            Open = 105, High = 115, Low = 100, Close = 110
        };

        // Act & Assert
        Assert.False(range1.IsContiguousWith(range2));
    }

    [Fact]
    public void IsContiguousWith_Overlapping_ReturnsTrue()
    {
        // Arrange: ranges overlap
        var range1 = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(2),
            Open = 100, High = 110, Low = 95, Close = 105
        };
        var range2 = new TimeRange
        {
            Start = BaseTime.AddHours(1),
            End = BaseTime.AddHours(3),
            Open = 105, High = 115, Low = 100, Close = 110
        };

        // Act & Assert
        Assert.True(range1.IsContiguousWith(range2));
    }

    [Fact]
    public void Merge_CombinesOhlcCorrectly()
    {
        // Arrange
        var earlier = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 90, Close = 105,
            SourceId = "A"
        };
        var later = new TimeRange
        {
            Start = BaseTime.AddHours(1),
            End = BaseTime.AddHours(2),
            Open = 105, High = 120, Low = 95, Close = 115,
            SourceId = "B"
        };

        // Act
        var merged = TimeRange.Merge(earlier, later);

        // Assert
        Assert.Equal(BaseTime, merged.Start);
        Assert.Equal(BaseTime.AddHours(2), merged.End);
        Assert.Equal(100, merged.Open);   // From earlier
        Assert.Equal(120, merged.High);   // Max of both
        Assert.Equal(90, merged.Low);     // Min of both
        Assert.Equal(115, merged.Close);  // From later
        Assert.Contains("A", merged.SourceId);
        Assert.Contains("B", merged.SourceId);
    }

    [Fact]
    public void Merge_ReordersIfCalledWithLaterFirst()
    {
        // Arrange
        var earlier = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 90, Close = 105
        };
        var later = new TimeRange
        {
            Start = BaseTime.AddHours(1),
            End = BaseTime.AddHours(2),
            Open = 105, High = 120, Low = 95, Close = 115
        };

        // Act - call with later first
        var merged = TimeRange.Merge(later, earlier);

        // Assert - should still get correct Open from earlier
        Assert.Equal(100, merged.Open);
        Assert.Equal(115, merged.Close);
    }

    [Fact]
    public void Merge_ThrowsOnNull()
    {
        var range = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 90, Close = 105
        };

        Assert.Throws<ArgumentNullException>(() => TimeRange.Merge(null!, range));
        Assert.Throws<ArgumentNullException>(() => TimeRange.Merge(range, null!));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var range = new TimeRange
        {
            Start = BaseTime,
            End = BaseTime.AddHours(1),
            Open = 100, High = 110, Low = 90, Close = 105
        };

        var str = range.ToString();

        Assert.Contains("09:00", str);
        Assert.Contains("10:00", str);
        Assert.Contains("100", str);
        Assert.Contains("110", str);
    }
}
