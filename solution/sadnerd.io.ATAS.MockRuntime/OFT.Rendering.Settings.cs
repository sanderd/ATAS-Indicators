using OFT.Rendering.Tools;

namespace OFT.Rendering.Settings;

/// <summary>
/// Drawing layout modes matching ATAS signature
/// </summary>
public enum DrawingLayouts
{
    Historical,
    LatestBar,
    Final
}

/// <summary>
/// Pen settings for drawing lines and borders
/// Stores CrossColor (WPF Color) as set by indicators, converts to System.Drawing.Color for rendering
/// </summary>
public class PenSettings
{
    public CrossColor Color { get; set; } = CrossColors.White;
    public int Width { get; set; } = 1;
    
    private RenderPen? _renderObject;
    public RenderPen RenderObject
    {
        get
        {
            // Convert CrossColor (WPF) to System.Drawing.Color for rendering
            var drawingColor = System.Drawing.Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
            return _renderObject ??= new RenderPen(drawingColor, Width);
        }
    }
}
