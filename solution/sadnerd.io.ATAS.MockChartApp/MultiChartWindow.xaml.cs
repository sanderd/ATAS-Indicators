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
            
            // Set days to load (do this before adding indicators so data is already loaded)
            chart.DaysToLoad = config.DaysToLoad;
            
            // Add indicators based on config
            foreach (var indicatorName in config.Indicators)
            {
                Indicator? indicator = indicatorName switch
                {
                    "KeyLevels" => new sadnerd.io.ATAS.KeyLevels.KeyLevels(),
                    "PvsraCandles" => new sadnerd.io.ATAS.PvsraCandles.PvsraCandles(),
                    "EmaWithCloud" => new sadnerd.io.ATAS.EmaWithCloud.EmaWithCloud(),
                    _ => null
                };
                
                if (indicator != null)
                {
                    // Apply saved settings if available
                    var savedSettings = config.IndicatorSettings.FirstOrDefault(s => s.Name == indicatorName);
                    if (savedSettings != null)
                    {
                        ApplyIndicatorSettings(indicator, savedSettings);
                    }
                    
                    chart.AddIndicator(indicator);
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
                DaysToLoad = chart.DaysToLoad,
                Indicators = chart.GetActiveIndicatorNames(),
                IndicatorSettings = GetIndicatorSettings(chart)
            }).ToList()
        };
        
        ChartLayoutManager.SaveLayout(layout);
    }

    private List<IndicatorConfig> GetIndicatorSettings(ChartPanel chart)
    {
        var settings = new List<IndicatorConfig>();
        
        foreach (var indicator in chart.ActiveIndicators)
        {
            var config = new IndicatorConfig { Name = indicator.GetType().Name };
            
            // Get properties with Display attribute
            var properties = indicator.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), true).Any()
                         && p.CanRead && p.CanWrite);
                         
            foreach (var prop in properties)
            {
                var value = prop.GetValue(indicator);
                if (value != null)
                {
                    // Convert CrossColor to serializable format
                    if (prop.PropertyType == typeof(CrossColor))
                    {
                        var color = (CrossColor)value;
                        config.Settings[prop.Name] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                    }
                    else
                    {
                        config.Settings[prop.Name] = value;
                    }
                }
            }
            
            settings.Add(config);
        }
        
        return settings;
    }

    private void ApplyIndicatorSettings(Indicator indicator, IndicatorConfig config)
    {
        foreach (var kvp in config.Settings)
        {
            var prop = indicator.GetType().GetProperty(kvp.Key);
            if (prop == null || !prop.CanWrite || kvp.Value == null)
                continue;
                
            try
            {
                if (prop.PropertyType == typeof(CrossColor) && kvp.Value is string colorStr)
                {
                    // Parse color from #AARRGGBB format
                    if (colorStr.StartsWith("#") && colorStr.Length == 9)
                    {
                        byte a = Convert.ToByte(colorStr.Substring(1, 2), 16);
                        byte r = Convert.ToByte(colorStr.Substring(3, 2), 16);
                        byte g = Convert.ToByte(colorStr.Substring(5, 2), 16);
                        byte b = Convert.ToByte(colorStr.Substring(7, 2), 16);
                        prop.SetValue(indicator, CrossColor.FromArgb(a, r, g, b));
                    }
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(indicator, Convert.ToBoolean(kvp.Value));
                }
                else if (prop.PropertyType == typeof(int))
                {
                    prop.SetValue(indicator, Convert.ToInt32(kvp.Value));
                }
                else if (prop.PropertyType == typeof(decimal))
                {
                    prop.SetValue(indicator, Convert.ToDecimal(kvp.Value));
                }
            }
            catch
            {
                // Ignore conversion errors
            }
        }
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
            IndicatorPropertiesPanel.Children.Clear();
            return;
        }
        
        SelectedChartLabel.Text = $"Selected: {_selectedChart.Symbol} {_selectedChart.Timeframe.ToDisplayString()}";
        
        // Set days combo
        var days = _selectedChart.DaysToLoad;
        for (int i = 0; i < DaysCombo.Items.Count; i++)
        {
            if (DaysCombo.Items[i] is ComboBoxItem item && 
                int.TryParse(item.Content?.ToString(), out int itemDays) && 
                itemDays == days)
            {
                DaysCombo.SelectedIndex = i;
                break;
            }
        }
        
        var indicators = _selectedChart.GetActiveIndicatorNames();
        KeyLevelsCheck.IsChecked = indicators.Contains("KeyLevels");
        PvsraCandlesCheck.IsChecked = indicators.Contains("PvsraCandles");
        EmaCloudCheck.IsChecked = indicators.Contains("EmaWithCloud");
        
        // Generate dynamic indicator property UI
        GenerateIndicatorPropertiesUI();
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
        
        // Regenerate property UI after indicator change
        GenerateIndicatorPropertiesUI();
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
            
            // Copy days setting
            chart.DaysToLoad = _selectedChart.DaysToLoad;
        }
    }

    private void DaysCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedChart == null || DaysCombo.SelectedItem is not ComboBoxItem item)
            return;
            
        if (int.TryParse(item.Content?.ToString(), out int days))
        {
            _selectedChart.DaysToLoad = days;
        }
    }

    private void GenerateIndicatorPropertiesUI()
    {
        IndicatorPropertiesPanel.Children.Clear();
        
        if (_selectedChart == null)
            return;
            
        foreach (var indicator in _selectedChart.ActiveIndicators)
        {
            var indicatorName = indicator.GetType().Name;
            
            // Add indicator header
            var header = new TextBlock
            {
                Text = indicatorName,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 187, 106)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            IndicatorPropertiesPanel.Children.Add(header);
            
            // Find properties with Display attribute
            var properties = indicator.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), true).Any()
                         && p.CanRead && p.CanWrite)
                .ToList();
                
            foreach (var prop in properties)
            {
                var displayAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), true)
                    .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;
                    
                var displayName = displayAttr?.Name ?? prop.Name;
                var propValue = prop.GetValue(indicator);
                
                // Create property row based on type
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                row.Children.Add(new TextBlock 
                { 
                    Text = displayName, 
                    Foreground = System.Windows.Media.Brushes.White, 
                    Width = 140,
                    VerticalAlignment = VerticalAlignment.Center
                });
                
                if (prop.PropertyType == typeof(bool))
                {
                    var checkBox = new CheckBox { IsChecked = (bool?)propValue ?? false, Tag = (indicator, prop) };
                    checkBox.Click += PropertyCheckBox_Click;
                    row.Children.Add(checkBox);
                }
                else if (prop.PropertyType == typeof(int))
                {
                    var textBox = new TextBox { Text = propValue?.ToString() ?? "0", Width = 60, Tag = (indicator, prop) };
                    textBox.LostFocus += PropertyIntTextBox_LostFocus;
                    row.Children.Add(textBox);
                }
                else if (prop.PropertyType == typeof(decimal))
                {
                    var textBox = new TextBox { Text = propValue?.ToString() ?? "0", Width = 80, Tag = (indicator, prop) };
                    textBox.LostFocus += PropertyDecimalTextBox_LostFocus;
                    row.Children.Add(textBox);
                }
                else if (prop.PropertyType == typeof(CrossColor))
                {
                    var colorValue = propValue as CrossColor? ?? CrossColors.White;
                    var colorBtn = new Button 
                    { 
                        Width = 60, 
                        Height = 20,
                        Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(colorValue.A, colorValue.R, colorValue.G, colorValue.B)),
                        Tag = (indicator, prop),
                        Content = ""
                    };
                    // Color picker would require more complex implementation, skip for now
                    row.Children.Add(colorBtn);
                }
                
                IndicatorPropertiesPanel.Children.Add(row);
            }
        }
    }

    private void PropertyCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is (Indicator indicator, System.Reflection.PropertyInfo prop))
        {
            prop.SetValue(indicator, cb.IsChecked ?? false);
            _selectedChart?.Refresh();
        }
    }

    private void PropertyIntTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is (Indicator indicator, System.Reflection.PropertyInfo prop))
        {
            if (int.TryParse(tb.Text, out int value))
            {
                prop.SetValue(indicator, value);
                _selectedChart?.Refresh();
            }
        }
    }

    private void PropertyDecimalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is (Indicator indicator, System.Reflection.PropertyInfo prop))
        {
            if (decimal.TryParse(tb.Text, out decimal value))
            {
                prop.SetValue(indicator, value);
                _selectedChart?.Refresh();
            }
        }
    }

    #endregion
}
