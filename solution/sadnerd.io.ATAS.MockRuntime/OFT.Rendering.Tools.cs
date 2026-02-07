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
