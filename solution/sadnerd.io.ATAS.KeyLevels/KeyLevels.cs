using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;
using Color = System.Drawing.Color;

namespace sadnerd.io.ATAS.KeyLevels
{
    /// <summary>
    /// Anchor position for the key levels display
    /// </summary>
    public enum AnchorPosition
    {
        [Display(Name = "Left")]
        Left,

        [Display(Name = "Right")]
        Right,

        [Display(Name = "Last Bar")]
        LastBar
    }

    /// <summary>
    /// Represents a key price level with a label
    /// </summary>
    public class KeyLevel
    {
        public decimal Price { get; set; }
        public string Label { get; set; }

        public KeyLevel(decimal price, string label)
        {
            Price = price;
            Label = label;
        }
    }

    [DisplayName("Key Levels")]
    [Display(Name = "Key Levels", Description = "Displays key price levels on the chart")]
    [HelpLink("https://github.com/sanderd/ATAS-Indicators/wiki/Key-Levels")]
    public class KeyLevels : Indicator
    {
        #region Fields

        private int _fontSize = 10;
        private AnchorPosition _anchor = AnchorPosition.LastBar;
        private int _distanceFromAnchor = 10;
        private CrossColor _textColor = CrossColors.White;
        private CrossColor _lineColor = CrossColors.White;
        private CrossColor _backgroundColor = CrossColor.FromArgb(128, 40, 40, 40);
        private int _lineWidth = 50;

        private readonly RenderStringFormat _labelFormat = new()
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        // Hardcoded levels for now
        private readonly List<KeyLevel> _levels = new()
        {
            new KeyLevel(25020, "Test1"),
            new KeyLevel(24975, "Test2")
        };

        #endregion

        #region Properties

        [Display(Name = "Font Size", GroupName = "Drawing", Order = 10)]
        [Range(6, 24)]
        public int FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Anchor", GroupName = "Drawing", Order = 20)]
        public AnchorPosition Anchor
        {
            get => _anchor;
            set
            {
                _anchor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Distance from Anchor", GroupName = "Drawing", Order = 30)]
        [Range(-500, 500)]
        public int DistanceFromAnchor
        {
            get => _distanceFromAnchor;
            set
            {
                _distanceFromAnchor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Text Color", GroupName = "Drawing", Order = 40)]
        public CrossColor TextColor
        {
            get => _textColor;
            set
            {
                _textColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Line Color", GroupName = "Drawing", Order = 50)]
        public CrossColor LineColor
        {
            get => _lineColor;
            set
            {
                _lineColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Background Color", GroupName = "Drawing", Order = 60)]
        public CrossColor BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Line Width", GroupName = "Drawing", Order = 70)]
        [Range(10, 200)]
        public int LineWidth
        {
            get => _lineWidth;
            set
            {
                _lineWidth = value;
                RecalculateValues();
            }
        }

        #endregion

        #region Constructor

        public KeyLevels() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
            DrawAbovePrice = true;

            DataSeries[0].IsHidden = true;
        }

        #endregion

        #region Overrides

        protected override void OnCalculate(int bar, decimal value)
        {
            // No calculation needed for static key levels
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null || Container is null)
                return;

            var region = Container.Region;
            var font = new RenderFont("Arial", FontSize);

            // Calculate anchor X position
            int anchorX = CalculateAnchorX(region);

            // Draw background rectangle from top to bottom at anchor position
            int rectWidth = _lineWidth + 100; // Line width + space for text
            int rectX = anchorX;

            // Adjust rectangle position based on anchor
            if (Anchor == AnchorPosition.Right)
            {
                rectX = anchorX - rectWidth;
            }

            var backgroundRect = new Rectangle(rectX, region.Top, rectWidth, region.Height);
            context.FillRectangle(BackgroundColor.Convert(), backgroundRect);

            // Draw each level
            foreach (var level in _levels)
            {
                DrawLevel(context, font, level, anchorX, region);
            }
        }

        #endregion

        #region Private Methods

        private int CalculateAnchorX(Rectangle region)
        {
            int baseX = Anchor switch
            {
                AnchorPosition.Left => region.Left,
                AnchorPosition.Right => region.Right,
                AnchorPosition.LastBar => ChartInfo.GetXByBar(LastVisibleBarNumber),
                _ => region.Left
            };

            return baseX + DistanceFromAnchor;
        }

        private void DrawLevel(RenderContext context, RenderFont font, KeyLevel level, int anchorX, Rectangle region)
        {
            // Get Y coordinate for this price level
            int y = ChartInfo.GetYByPrice(level.Price, false);

            // Check if the level is visible on the chart
            if (y < region.Top || y > region.Bottom)
                return;

            // Calculate line start and end positions
            int lineStartX;
            int lineEndX;
            int textX;

            if (Anchor == AnchorPosition.Right)
            {
                lineEndX = anchorX;
                lineStartX = anchorX - _lineWidth;
                textX = lineStartX - 5; // Text to the left of the line
            }
            else
            {
                lineStartX = anchorX;
                lineEndX = anchorX + _lineWidth;
                textX = lineEndX + 5; // Text to the right of the line
            }

            // Draw the horizontal line at the price level
            var linePen = new RenderPen(LineColor.Convert(), 2);
            context.DrawLine(linePen, lineStartX, y, lineEndX, y);

            // Draw the text label
            var textSize = context.MeasureString(level.Label, font);
            
            Rectangle textRect;
            if (Anchor == AnchorPosition.Right)
            {
                // For right anchor, draw text to the left of the line
                textRect = new Rectangle(textX - textSize.Width, y - textSize.Height / 2, textSize.Width, textSize.Height);
            }
            else
            {
                // For left/lastbar anchor, draw text to the right of the line
                textRect = new Rectangle(textX, y - textSize.Height / 2, textSize.Width, textSize.Height);
            }

            context.DrawString(level.Label, font, TextColor.Convert(), textRect, _labelFormat);
        }

        #endregion
    }
}
