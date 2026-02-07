using System.Windows;
using System.Windows.Controls;
using ATAS.Indicators;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Multi-chart window with configurable layouts
/// </summary>
public partial class MultiChartWindow : Window
{
    private readonly List<ChartPanel> _chartPanels = new();
    private int _rows = 2;
    private int _cols = 2;

    public MultiChartWindow()
    {
        InitializeComponent();
        
        // Initialize with 2x2 layout
        SetLayout(2, 2);
    }

    #region Layout Management

    private void SetLayout(int rows, int cols)
    {
        _rows = rows;
        _cols = cols;
        
        // Clear existing
        ChartContainer.Children.Clear();
        ChartContainer.RowDefinitions.Clear();
        ChartContainer.ColumnDefinitions.Clear();
        _chartPanels.Clear();
        
        // Create grid
        for (int r = 0; r < rows; r++)
        {
            ChartContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        for (int c = 0; c < cols; c++)
        {
            ChartContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        
        // Create chart panels
        var timeframes = new[] { Timeframe.H1, Timeframe.M15, Timeframe.H4, Timeframe.Daily, Timeframe.M5, Timeframe.M30 };
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int index = r * cols + c;
                var chart = new ChartPanel
                {
                    Symbol = "ES",
                    Margin = new Thickness(1)
                };
                
                // Set different timeframe for each
                if (index < timeframes.Length)
                {
                    chart.Timeframe = timeframes[index];
                }
                
                // Add KeyLevels indicator
                var keyLevels = new sadnerd.io.ATAS.KeyLevels.KeyLevels();
                chart.AddIndicator(keyLevels);
                
                Grid.SetRow(chart, r);
                Grid.SetColumn(chart, c);
                ChartContainer.Children.Add(chart);
                _chartPanels.Add(chart);
            }
        }
    }

    private void Layout1x1_Click(object sender, RoutedEventArgs e) => SetLayout(1, 1);
    private void Layout2x1_Click(object sender, RoutedEventArgs e) => SetLayout(1, 2);
    private void Layout2x2_Click(object sender, RoutedEventArgs e) => SetLayout(2, 2);
    private void Layout3x2_Click(object sender, RoutedEventArgs e) => SetLayout(2, 3);

    private void AddChart_Click(object sender, RoutedEventArgs e)
    {
        // Add a column to current layout
        int newCols = _cols + 1;
        if (newCols > 4) newCols = 1; // Wrap around
        SetLayout(_rows, newCols);
    }

    #endregion
}
