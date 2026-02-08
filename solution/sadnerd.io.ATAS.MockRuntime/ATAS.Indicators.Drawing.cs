using System.Collections.Generic;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

namespace ATAS.Indicators.Drawing;

/// <summary>
/// TrendLine for drawing lines/rays on the chart
/// </summary>
public class TrendLine
{
    public int FirstBar { get; set; }
    public decimal FirstPrice { get; set; }
    public int SecondBar { get; set; }
    public decimal SecondPrice { get; set; }
    public bool IsRay { get; set; }
    public CrossPen Pen { get; set; }

    /// <summary>
    /// Constructor matching ATAS API signature with pen parameter
    /// </summary>
    public TrendLine(int firstBar, decimal firstPrice, int secondBar, decimal secondPrice, CrossPen pen)
    {
        FirstBar = firstBar;
        FirstPrice = firstPrice;
        SecondBar = secondBar;
        SecondPrice = secondPrice;
        Pen = pen;
    }
}

/// <summary>
/// Collection of trend lines
/// </summary>
public class TrendLinesCollection : List<TrendLine>
{
}
