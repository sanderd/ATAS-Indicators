using System.Windows;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for multi-chart mode
        bool multiChart = e.Args.Contains("--multi") || e.Args.Contains("-m");

        if (multiChart)
        {
            MainWindow = new MultiChartWindow();
        }
        else
        {
            MainWindow = new MainWindow();
        }
        
        MainWindow.Show();
    }
}
