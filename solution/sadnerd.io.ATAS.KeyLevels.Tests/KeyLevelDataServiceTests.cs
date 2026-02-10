using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using sadnerd.io.ATAS.KeyLevels.DataStore;
using Xunit;

namespace sadnerd.io.ATAS.KeyLevels.Tests;

/// <summary>
/// Unit tests for the KeyLevelDataService class.
/// </summary>
public class KeyLevelDataServiceTests : IDisposable
{
    public KeyLevelDataServiceTests()
    {
        // Reset service before each test
        KeyLevelDataService.Instance.Reset();
    }

    public void Dispose()
    {
        // Clean up after each test
        KeyLevelDataService.Instance.Reset();
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = KeyLevelDataService.Instance;
        var instance2 = KeyLevelDataService.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetStore_CreatesNewStore()
    {
        var service = KeyLevelDataService.Instance;

        var store = service.GetStore("ES");

        Assert.NotNull(store);
        Assert.Equal("ES", store.Symbol);
    }

    [Fact]
    public void GetStore_ReturnsSameStoreForSameSymbol()
    {
        var service = KeyLevelDataService.Instance;

        var store1 = service.GetStore("ES");
        var store2 = service.GetStore("ES");

        Assert.Same(store1, store2);
    }

    [Fact]
    public void GetStore_ReturnsDifferentStoresForDifferentSymbols()
    {
        var service = KeyLevelDataService.Instance;

        var esStore = service.GetStore("ES");
        var nqStore = service.GetStore("NQ");

        Assert.NotSame(esStore, nqStore);
        Assert.Equal("ES", esStore.Symbol);
        Assert.Equal("NQ", nqStore.Symbol);
    }

    [Fact]
    public void GetStore_ThrowsOnNullOrEmpty()
    {
        var service = KeyLevelDataService.Instance;

        Assert.Throws<ArgumentNullException>(() => service.GetStore(null!));
        Assert.Throws<ArgumentNullException>(() => service.GetStore(""));
    }

    [Fact]
    public void HasStore_ReturnsFalseForUnknown()
    {
        var service = KeyLevelDataService.Instance;

        Assert.False(service.HasStore("UNKNOWN"));
    }

    [Fact]
    public void HasStore_ReturnsTrueAfterGetStore()
    {
        var service = KeyLevelDataService.Instance;
        service.GetStore("ES");

        Assert.True(service.HasStore("ES"));
    }

    [Fact]
    public void GetAllSymbols_ReturnsCreatedSymbols()
    {
        var service = KeyLevelDataService.Instance;
        service.GetStore("ES");
        service.GetStore("NQ");
        service.GetStore("CL");

        var symbols = service.GetAllSymbols();

        Assert.Equal(3, symbols.Count);
        Assert.Contains("ES", symbols);
        Assert.Contains("NQ", symbols);
        Assert.Contains("CL", symbols);
    }

    [Fact]
    public void Reset_ClearsAllStores()
    {
        var service = KeyLevelDataService.Instance;
        service.GetStore("ES");
        service.GetStore("NQ");

        service.Reset();

        Assert.False(service.HasStore("ES"));
        Assert.False(service.HasStore("NQ"));
        Assert.Equal(0, service.StoreCount);
    }

    [Fact]
    public void StoreCount_ReturnsCorrectCount()
    {
        var service = KeyLevelDataService.Instance;
        Assert.Equal(0, service.StoreCount);

        service.GetStore("ES");
        Assert.Equal(1, service.StoreCount);

        service.GetStore("NQ");
        Assert.Equal(2, service.StoreCount);

        service.GetStore("ES"); // Same symbol, no increase
        Assert.Equal(2, service.StoreCount);
    }

    [Fact]
    public void ConcurrentGetStore_IsThreadSafe()
    {
        var service = KeyLevelDataService.Instance;
        var stores = new System.Collections.Concurrent.ConcurrentBag<InstrumentDataStore>();

        // Create 100 concurrent accesses to the same symbol
        Parallel.For(0, 100, _ =>
        {
            stores.Add(service.GetStore("ES"));
        });

        // All should be the same instance
        var firstStore = stores.First();
        Assert.All(stores, store => Assert.Same(firstStore, store));
        Assert.Equal(1, service.StoreCount);
    }
}
