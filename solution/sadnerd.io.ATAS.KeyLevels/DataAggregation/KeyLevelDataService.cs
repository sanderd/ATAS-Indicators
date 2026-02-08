using System.Collections.Concurrent;

namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Service locator for accessing aggregated key level data.
/// Singleton pattern provides cross-instance access to shared data stores.
/// </summary>
public sealed class KeyLevelDataService
{
    private static readonly Lazy<KeyLevelDataService> _instance = new(() => new KeyLevelDataService());
    
    /// <summary>
    /// Gets the singleton instance of the service.
    /// </summary>
    public static KeyLevelDataService Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, InstrumentDataStore> _stores = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern.
    /// </summary>
    private KeyLevelDataService() { }

    /// <summary>
    /// Gets or creates a data store for the specified instrument symbol.
    /// </summary>
    /// <param name="symbol">The instrument symbol (e.g., "ES", "NQ").</param>
    /// <returns>The InstrumentDataStore for this symbol.</returns>
    public InstrumentDataStore GetStore(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentNullException(nameof(symbol));
        
        return _stores.GetOrAdd(symbol, s => new InstrumentDataStore(s));
    }

    /// <summary>
    /// Checks if a store exists for the specified symbol (without creating it).
    /// </summary>
    /// <param name="symbol">The instrument symbol.</param>
    /// <returns>True if a store exists.</returns>
    public bool HasStore(string symbol)
    {
        return _stores.ContainsKey(symbol);
    }

    /// <summary>
    /// Gets all symbols that have data stores.
    /// </summary>
    /// <returns>Collection of instrument symbols.</returns>
    public IReadOnlyCollection<string> GetAllSymbols()
    {
        return _stores.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Resets all data stores. Primarily for testing purposes.
    /// </summary>
    public void Reset()
    {
        _stores.Clear();
    }

    /// <summary>
    /// Gets the number of active instrument stores.
    /// </summary>
    public int StoreCount => _stores.Count;
}
