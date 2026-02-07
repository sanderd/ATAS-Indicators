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
    private ChartPanel? _selectedChart;
    private int _rows = 2;
    private int _cols = 2;

    public MultiChartWindow()
    {
        InitializeComponent();
        
        // Initialize with 1x2 layout (2 side-by-side charts)
        SetLayout(1, 2);
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
        _selectedChart = null;
        
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
                
                // Hook up events
                chart.OnChartClicked += Chart_OnChartClicked;
                chart.OnRemoveRequested += Chart_OnRemoveRequested;
                
                Grid.SetRow(chart, r);
                Grid.SetColumn(chart, c);
                ChartContainer.Children.Add(chart);
                _chartPanels.Add(chart);
            }
        }
        
        // Select first chart by default
        if (_chartPanels.Count > 0)
        {
            SelectChart(_chartPanels[0]);
        }
    }

    private void SelectChart(ChartPanel chart)
    {
        // Deselect previous
        if (_selectedChart != null)
        {
            _selectedChart.IsSelected = false;
        }
        
        // Select new
        _selectedChart = chart;
        chart.IsSelected = true;
    }

    private void Chart_OnChartClicked(object? sender, EventArgs e)
    {
        if (sender is ChartPanel chart)
        {
            SelectChart(chart);
        }
    }

    private void Chart_OnRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not ChartPanel chart)
            return;
            
        // Don't allow removing the last chart
        if (_chartPanels.Count <= 1)
        {
            MessageBox.Show("Cannot remove the last chart.", "Remove Chart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Remove from grid and list
        ChartContainer.Children.Remove(chart);
        _chartPanels.Remove(chart);
        
        // If we removed the selected chart, select another
        if (_selectedChart == chart && _chartPanels.Count > 0)
        {
            SelectChart(_chartPanels[0]);
        }
        
        // Reorganize grid layout
        ReorganizeGrid();
    }

    private void ReorganizeGrid()
    {
        // Simple reorganization: put all charts into current grid cells
        int index = 0;
        for (int r = 0; r < _rows && index < _chartPanels.Count; r++)
        {
            for (int c = 0; c < _cols && index < _chartPanels.Count; c++)
            {
                var chart = _chartPanels[index];
                Grid.SetRow(chart, r);
                Grid.SetColumn(chart, c);
                index++;
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
