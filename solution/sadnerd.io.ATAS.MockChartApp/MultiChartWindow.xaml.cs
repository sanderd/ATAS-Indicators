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
        
        // Load layout from disk or use default
        var layout = ChartLayoutManager.LoadLayout();
        ApplyLayout(layout);
        
        // Save layout when window closes
        Closing += (_, _) => SaveCurrentLayout();
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
        
        // Update settings panel if visible
        if (SettingsPanel.Visibility == Visibility.Visible)
        {
            UpdateSettingsPanelForSelectedChart();
        }
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

    private void ApplyLayout(LayoutConfig layout)
    {
        _rows = layout.Rows;
        _cols = layout.Cols;
        
        // Clear existing
        ChartContainer.Children.Clear();
        ChartContainer.RowDefinitions.Clear();
        ChartContainer.ColumnDefinitions.Clear();
        _chartPanels.Clear();
        _selectedChart = null;
        
        // Create grid
        for (int r = 0; r < _rows; r++)
        {
            ChartContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        for (int c = 0; c < _cols; c++)
        {
            ChartContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        
        // Create chart panels from config
        for (int i = 0; i < layout.Charts.Count && i < _rows * _cols; i++)
        {
            var config = layout.Charts[i];
            var chart = new ChartPanel
            {
                Symbol = config.Symbol,
                Timeframe = config.Timeframe,
                Margin = new Thickness(1)
            };
            
            // Add indicators based on config
            foreach (var indicatorName in config.Indicators)
            {
                if (indicatorName == "KeyLevels")
                {
                    chart.AddIndicator(new sadnerd.io.ATAS.KeyLevels.KeyLevels());
                }
                else if (indicatorName == "PvsraCandles")
                {
                    chart.AddIndicator(new sadnerd.io.ATAS.PvsraCandles.PvsraCandles());
                }
                else if (indicatorName == "EmaWithCloud")
                {
                    chart.AddIndicator(new sadnerd.io.ATAS.EmaWithCloud.EmaWithCloud());
                }
            }
            
            // Hook up events
            chart.OnChartClicked += Chart_OnChartClicked;
            chart.OnRemoveRequested += Chart_OnRemoveRequested;
            
            int row = i / _cols;
            int col = i % _cols;
            Grid.SetRow(chart, row);
            Grid.SetColumn(chart, col);
            ChartContainer.Children.Add(chart);
            _chartPanels.Add(chart);
        }
        
        // Select first chart by default
        if (_chartPanels.Count > 0)
        {
            SelectChart(_chartPanels[0]);
        }
    }

    private void SaveCurrentLayout()
    {
        var layout = new LayoutConfig
        {
            Rows = _rows,
            Cols = _cols,
            Charts = _chartPanels.Select(chart => new ChartConfig
            {
                Symbol = chart.Symbol,
                Timeframe = chart.Timeframe,
                Indicators = chart.GetActiveIndicatorNames()
            }).ToList()
        };
        
        ChartLayoutManager.SaveLayout(layout);
    }

    #endregion

    #region Settings Panel

    private void ToggleSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
        
        if (SettingsPanel.Visibility == Visibility.Visible)
        {
            UpdateSettingsPanelForSelectedChart();
        }
    }

    private void UpdateSettingsPanelForSelectedChart()
    {
        if (_selectedChart == null)
        {
            SelectedChartLabel.Text = "No chart selected";
            KeyLevelsCheck.IsChecked = false;
            PvsraCandlesCheck.IsChecked = false;
            EmaCloudCheck.IsChecked = false;
            return;
        }
        
        SelectedChartLabel.Text = $"Selected: {_selectedChart.Symbol} {_selectedChart.Timeframe.ToDisplayString()}";
        
        var indicators = _selectedChart.GetActiveIndicatorNames();
        KeyLevelsCheck.IsChecked = indicators.Contains("KeyLevels");
        PvsraCandlesCheck.IsChecked = indicators.Contains("PvsraCandles");
        EmaCloudCheck.IsChecked = indicators.Contains("EmaWithCloud");
    }

    private void IndicatorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChart == null || sender is not CheckBox checkBox)
            return;
            
        var indicatorName = checkBox.Tag?.ToString();
        if (string.IsNullOrEmpty(indicatorName))
            return;
            
        if (checkBox.IsChecked == true)
        {
            AddIndicatorToChart(_selectedChart, indicatorName);
        }
        else
        {
            _selectedChart.RemoveIndicatorByName(indicatorName);
        }
    }

    private void AddIndicatorToChart(ChartPanel chart, string indicatorName)
    {
        switch (indicatorName)
        {
            case "KeyLevels":
                chart.AddIndicator(new sadnerd.io.ATAS.KeyLevels.KeyLevels());
                break;
            case "PvsraCandles":
                chart.AddIndicator(new sadnerd.io.ATAS.PvsraCandles.PvsraCandles());
                break;
            case "EmaWithCloud":
                chart.AddIndicator(new sadnerd.io.ATAS.EmaWithCloud.EmaWithCloud());
                break;
        }
    }

    private void ApplyToAllCharts_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChart == null)
            return;
            
        var indicators = _selectedChart.GetActiveIndicatorNames();
        
        foreach (var chart in _chartPanels)
        {
            if (chart == _selectedChart)
                continue;
                
            // Clear and re-add indicators to match selected chart
            chart.ClearIndicators();
            foreach (var indicatorName in indicators)
            {
                AddIndicatorToChart(chart, indicatorName);
            }
        }
    }

    #endregion
}
