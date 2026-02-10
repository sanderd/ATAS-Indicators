using System.Drawing;
using SDA = System.Drawing.StringAlignment;

namespace OFT.Rendering.Tools;

/// <summary>
/// Type alias for StringAlignment (ATAS uses System.Drawing.StringAlignment)
/// </summary>
public enum StringAlignment
{
    Near = 0,
    Center = 1,
    Far = 2
}

/// <summary>
/// Mock RenderFont matching ATAS signature
/// </summary>
public class RenderFont
{
    public string FontFamily { get; }
    public float Size { get; }

    public RenderFont(string fontFamily, float size)
    {
        FontFamily = fontFamily;
        Size = size;
    }
}

/// <summary>
/// Mock RenderPen matching ATAS signature
/// </summary>
public class RenderPen
{
    public Color Color { get; }
    public float Width { get; }

    public RenderPen(Color color, float width = 1)
    {
        Color = color;
        Width = width;
    }
}

/// <summary>
/// Mock RenderStringFormat matching ATAS signature
/// </summary>
public class RenderStringFormat
{
    public SDA Alignment { get; set; } = SDA.Near;
    public SDA LineAlignment { get; set; } = SDA.Center;
}

/// <summary>
/// Mock Brush class matching System.Windows.Media.SolidColorBrush signature
/// </summary>
public class CrossBrush
{
    public CrossColor Color { get; set; }

    public CrossBrush(CrossColor color)
    {
        Color = color;
    }
}

/// <summary>
/// Pen class matching System.Drawing.Pen signature for ATAS indicators
/// </summary>
public class CrossPen
{
    public System.Drawing.Color Color { get; set; }
    public float Width { get; set; }

    /// <summary>
    /// Constructor matching System.Drawing.Pen(Color, float) signature
    /// </summary>
    public CrossPen(System.Drawing.Color color, float width)
    {
        Color = color;
        Width = width;
    }

    /// <summary>
    /// Convert to RenderPen for drawing
    /// </summary>
    public RenderPen ToRenderPen()
    {
        return new RenderPen(Color, Width);
    }
}
