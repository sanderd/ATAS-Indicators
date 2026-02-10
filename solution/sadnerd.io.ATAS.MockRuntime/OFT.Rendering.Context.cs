using System.Drawing;
using SkiaSharp;

namespace OFT.Rendering.Context;

/// <summary>
/// Mock RenderContext matching ATAS OFT.Rendering.Context.RenderContext signature
/// </summary>
public class RenderContext : IDisposable
{
    private readonly SKCanvas _canvas;
    private bool _disposed;

    public RenderContext(SKCanvas canvas)
    {
        _canvas = canvas;
    }

    /// <summary>
    /// Draw a line between two points
    /// </summary>
    public void DrawLine(OFT.Rendering.Tools.RenderPen pen, int x1, int y1, int x2, int y2)
    {
        using var paint = new SKPaint
        {
            Color = ToSkColor(pen.Color),
            StrokeWidth = pen.Width,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        _canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    /// <summary>
    /// Fill a rectangle with a solid color
    /// </summary>
    public void FillRectangle(Color color, Rectangle rect)
    {
        using var paint = new SKPaint
        {
            Color = ToSkColor(color),
            Style = SKPaintStyle.Fill
        };
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, paint);
    }

    /// <summary>
    /// Draw and fill a rectangle with border and fill color
    /// </summary>
    public void DrawFillRectangle(OFT.Rendering.Tools.RenderPen borderPen, Color fillColor, Rectangle rect)
    {
        // Fill 
        using var fillPaint = new SKPaint
        {
            Color = ToSkColor(fillColor),
            Style = SKPaintStyle.Fill
        };
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, fillPaint);
        
        // Border if pen is visible
        if (borderPen.Color.A > 0)
        {
            using var strokePaint = new SKPaint
            {
                Color = ToSkColor(borderPen.Color),
                StrokeWidth = borderPen.Width,
                Style = SKPaintStyle.Stroke
            };
            _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, strokePaint);
        }
    }

    /// <summary>
    /// Draw text at a specified location
    /// </summary>
    public void DrawString(string text, OFT.Rendering.Tools.RenderFont font, Color color, Rectangle rect, OFT.Rendering.Tools.RenderStringFormat format)
    {
        using var paint = new SKPaint
        {
            Color = ToSkColor(color),
            TextSize = font.Size,
            Typeface = SKTypeface.FromFamilyName(font.FontFamily),
            IsAntialias = true
        };

        // Calculate position based on alignment
        float x = rect.X;
        float y = rect.Y + rect.Height / 2 + paint.TextSize / 3; // Approximate vertical center

        if (format.Alignment == System.Drawing.StringAlignment.Center)
        {
            paint.TextAlign = SKTextAlign.Center;
            x = rect.X + rect.Width / 2;
        }
        else if (format.Alignment == System.Drawing.StringAlignment.Far)
        {
            paint.TextAlign = SKTextAlign.Right;
            x = rect.X + rect.Width;
        }

        _canvas.DrawText(text, x, y, paint);
    }

    /// <summary>
    /// Measure the size of a string
    /// </summary>
    public Size MeasureString(string text, OFT.Rendering.Tools.RenderFont font)
    {
        using var paint = new SKPaint
        {
            TextSize = font.Size,
            Typeface = SKTypeface.FromFamilyName(font.FontFamily)
        };

        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);

        return new Size((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
    }

    private static SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
