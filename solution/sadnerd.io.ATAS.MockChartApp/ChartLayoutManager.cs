using System.IO;
using System.Text.Json;
using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Configuration for indicator settings
/// </summary>
public class IndicatorConfig
{
    public string Name { get; set; } = "";
    public Dictionary<string, object?> Settings { get; set; } = new();
}

/// <summary>
/// Configuration for a single chart
/// </summary>
public class ChartConfig
{
    public string Symbol { get; set; } = "ES";
    public Timeframe Timeframe { get; set; } = Timeframe.H1;
    public int DaysToLoad { get; set; } = 5;
    public List<string> Indicators { get; set; } = new() { "KeyLevels" };
    public List<IndicatorConfig> IndicatorSettings { get; set; } = new();
}

/// <summary>
/// Layout configuration for the entire window
/// </summary>
public class LayoutConfig
{
    public int Rows { get; set; } = 1;
    public int Cols { get; set; } = 2;
    public List<ChartConfig> Charts { get; set; } = new();
}

/// <summary>
/// Manages saving and loading chart layout to disk
/// </summary>
public static class ChartLayoutManager
{
    private static string LayoutFilePath
    {
        get
        {
            // Save next to the exe, not in AppData
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "chart_layout.json");
        }
    }

    public static void SaveLayout(LayoutConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(LayoutFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
        }
    }

    public static LayoutConfig LoadLayout()
    {
        try
        {
            if (File.Exists(LayoutFilePath))
            {
                var json = File.ReadAllText(LayoutFilePath);
                return JsonSerializer.Deserialize<LayoutConfig>(json) ?? GetDefaultLayout();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load layout: {ex.Message}");
        }
        
        return GetDefaultLayout();
    }

    private static LayoutConfig GetDefaultLayout()
    {
        return new LayoutConfig
        {
            Rows = 1,
            Cols = 2,
            Charts = new List<ChartConfig>
            {
                new() { Symbol = "ES", Timeframe = Timeframe.H1, Indicators = new() { "KeyLevels", "PvsraCandles" } },
                new() { Symbol = "ES", Timeframe = Timeframe.M15, Indicators = new() { "KeyLevels", "PvsraCandles" } }
            }
        };
    }
}
