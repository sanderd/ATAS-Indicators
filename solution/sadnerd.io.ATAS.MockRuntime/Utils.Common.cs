using System.Drawing;
using WpfColor = System.Windows.Media.Color;

namespace Utils.Common;

/// <summary>
/// Extension methods for color conversion (matching ATAS Utils.Common)
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Convert System.Windows.Media.Color to System.Drawing.Color
    /// </summary>
    public static Color Convert(this WpfColor color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
