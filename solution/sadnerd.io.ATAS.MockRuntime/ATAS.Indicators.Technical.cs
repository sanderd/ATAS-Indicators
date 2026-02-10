namespace ATAS.Indicators.Technical;

/// <summary>
/// Exponential Moving Average - Mock implementation
/// </summary>
public class EMA
{
    private readonly Dictionary<int, decimal> _values = new();
    private decimal _multiplier;
    private bool _initialized;
    
    public int Period { get; set; } = 14;
    
    public decimal this[int bar] => _values.TryGetValue(bar, out var val) ? val : 0m;

    public void Calculate(int bar, decimal value)
    {
        if (!_initialized || Period <= 0)
        {
            _multiplier = 2m / (Period + 1);
            _initialized = true;
        }

        if (bar == 0 || !_values.ContainsKey(bar - 1))
        {
            _values[bar] = value;
        }
        else
        {
            _values[bar] = (value - _values[bar - 1]) * _multiplier + _values[bar - 1];
        }
    }
}

/// <summary>
/// Standard Deviation - Mock implementation
/// </summary>
public class StdDev
{
    private readonly Dictionary<int, decimal> _values = new();
    private readonly List<decimal> _buffer = new();
    
    public int Period { get; set; } = 14;
    
    public decimal this[int bar] => _values.TryGetValue(bar, out var val) ? val : 0m;

    public void Calculate(int bar, decimal value)
    {
        _buffer.Add(value);
        
        if (_buffer.Count > Period)
        {
            _buffer.RemoveAt(0);
        }

        if (_buffer.Count < Period)
        {
            _values[bar] = 0;
            return;
        }

        decimal mean = _buffer.Average(x => x);
        decimal sumSquares = _buffer.Sum(x => (x - mean) * (x - mean));
        _values[bar] = (decimal)Math.Sqrt((double)(sumSquares / Period));
    }
}
