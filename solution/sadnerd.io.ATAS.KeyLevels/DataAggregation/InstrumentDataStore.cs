using System.Collections.Concurrent;
using sadnerd.io.ATAS.KeyLevels.DataStore;

namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Stores Points of Interest (POI) for a single instrument.
/// Thread-safe for concurrent access from multiple indicator instances.
/// </summary>
public class InstrumentDataStore
{
    // Key: (PeriodType, IsCurrent) -> PeriodPoi
    private readonly ConcurrentDictionary<(PeriodType, bool), PeriodPoi> _periods = new();

    /// <summary>
    /// The instrument symbol this store is for.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Creates a new data store for the specified instrument.
    /// </summary>
    /// <param name="symbol">The instrument symbol.</param>
    public InstrumentDataStore(string symbol)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }

    /// <summary>
    /// Contributes a time range of data for a period.
    /// The range will be merged with any existing data for that period.
    /// </summary>
    /// <param name="periodType">The type of period (Daily, Weekly, etc.).</param>
    /// <param name="isCurrent">True for current period, false for previous period.</param>
    /// <param name="periodStart">When this period starts.</param>
    /// <param name="periodEnd">When this period ends (use DateTime.MaxValue for ongoing).</param>
    /// <param name="contribution">The time range data to contribute.</param>
    public void ContributePeriodData(
        PeriodType periodType,
        bool isCurrent,
        DateTime periodStart,
        DateTime periodEnd,
        TimeRange contribution)
    {
        if (contribution == null) throw new ArgumentNullException(nameof(contribution));

        var key = (periodType, isCurrent);
        
        var poi = _periods.GetOrAdd(key, _ => new PeriodPoi
        {
            Type = periodType,
            IsCurrent = isCurrent,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        });

        // Check if period boundaries have changed (new period started)
        if (poi.PeriodStart != periodStart || poi.PeriodEnd != periodEnd)
        {
            // Period has changed, create new POI
            var newPoi = new PeriodPoi
            {
                Type = periodType,
                IsCurrent = isCurrent,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
            newPoi.AddContribution(contribution);
            _periods[key] = newPoi;
        }
        else
        {
            poi.AddContribution(contribution);
        }
    }

    /// <summary>
    /// Gets the aggregated POI for a period.
    /// </summary>
    /// <param name="type">The type of period.</param>
    /// <param name="isCurrent">True for current period, false for previous period.</param>
    /// <returns>The PeriodPoi, or null if no data has been contributed.</returns>
    public PeriodPoi? GetPeriodPoi(PeriodType type, bool isCurrent)
    {
        return _periods.TryGetValue((type, isCurrent), out var poi) ? poi : null;
    }

    /// <summary>
    /// Checks if we have complete coverage for a period.
    /// </summary>
    /// <param name="type">The type of period.</param>
    /// <param name="isCurrent">True for current period, false for previous period.</param>
    /// <returns>True if the period has complete data coverage.</returns>
    public bool HasCompleteCoverage(PeriodType type, bool isCurrent)
    {
        var poi = GetPeriodPoi(type, isCurrent);
        return poi?.HasCompleteCoverage() ?? false;
    }

    /// <summary>
    /// Gets a list of all periods that have been contributed to this store.
    /// </summary>
    /// <returns>List of period keys with their coverage status.</returns>
    public List<(PeriodType Type, bool IsCurrent, bool HasComplete)> GetAllPeriods()
    {
        return _periods
            .Select(kvp => (kvp.Key.Item1, kvp.Key.Item2, kvp.Value.HasCompleteCoverage()))
            .OrderBy(x => x.Item1)
            .ThenByDescending(x => x.Item2) // Current before Previous
            .ToList();
    }

    /// <summary>
    /// Clears all stored data. Primarily for testing.
    /// </summary>
    public void Clear()
    {
        _periods.Clear();
    }

    /// <summary>
    /// Returns a string representation of this store.
    /// </summary>
    public override string ToString() =>
        $"InstrumentDataStore[{Symbol}]: {_periods.Count} periods";
}
