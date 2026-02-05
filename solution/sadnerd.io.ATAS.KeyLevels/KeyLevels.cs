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
    /// Represents a key price level with a label and color
    /// </summary>
    public class KeyLevel
    {
        public decimal Price { get; set; }
        public string Label { get; set; }
        public CrossColor Color { get; set; }

        public KeyLevel(decimal price, string label, CrossColor color)
        {
            Price = price;
            Label = label;
            Color = color;
        }
    }

    /// <summary>
    /// Tracks high, low, open, close for a time period
    /// </summary>
    internal class PeriodRange
    {
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Mid => Low + (High - Low) / 2;
        public DateTime StartTime { get; set; }
        public int StartBar { get; set; } = -1;
        public bool IsValid => StartBar >= 0;

        public void Reset()
        {
            Open = High = Low = Close = 0;
            StartBar = -1;
        }

        public void Initialize(IndicatorCandle candle, int bar)
        {
            StartBar = bar;
            StartTime = candle.Time;
            Open = candle.Open;
            High = candle.High;
            Low = candle.Low;
            Close = candle.Close;
        }

        public void Update(IndicatorCandle candle)
        {
            if (candle.High > High) High = candle.High;
            if (candle.Low < Low) Low = candle.Low;
            Close = candle.Close;
        }
    }

    [DisplayName("Key Levels")]
    [Display(Name = "Key Levels", Description = "Displays key price levels on the chart")]
    [HelpLink("https://github.com/sanderd/ATAS-Indicators/wiki/Key-Levels")]
    public class KeyLevels : Indicator
    {
        #region Constants

        private const int FourHoursInMinutes = 240;

        #endregion

        #region Fields - Drawing

        private int _fontSize = 10;
        private AnchorPosition _anchor = AnchorPosition.LastBar;
        private int _distanceFromAnchor = 10;
        private CrossColor _textColor = CrossColors.White;
        private CrossColor _backgroundColor = CrossColor.FromArgb(128, 40, 40, 40);
        private int _lineWidth = 50;
        private int _backgroundWidth = 150;
        private bool _useShortLabels = true;

        #endregion

        #region Fields - Level Visibility

        // 4H visibility
        private bool _show4hOpen = true;
        private bool _show4hHighLow = true;

        // Daily visibility
        private bool _showDailyOpen = true;
        private bool _showPrevDayHighLow = true;
        private bool _showPrevDayMid = true;

        // Monday visibility
        private bool _showMondayHighLow = true;
        private bool _showMondayMid = true;

        // Quarterly visibility
        private bool _showQuarterlyOpen = true;
        private bool _showPrevQuarterHighLow = true;
        private bool _showPrevQuarterMid = true;

        private readonly RenderStringFormat _labelFormat = new()
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        #endregion

        #region Fields - Period Tracking

        // 4-hour periods
        private readonly PeriodRange _current4h = new();
        private readonly PeriodRange _previous4h = new();
        private DateTime _last4hPeriodStart;

        // Daily periods
        private readonly PeriodRange _currentDay = new();
        private readonly PeriodRange _previousDay = new();
        private DateTime _lastDayStart;

        // Monday (weekly)
        private readonly PeriodRange _currentMonday = new();
        private readonly PeriodRange _previousMonday = new();
        private DateTime _lastMondayStart;

        // Quarterly
        private readonly PeriodRange _currentQuarter = new();
        private readonly PeriodRange _previousQuarter = new();
        private int _lastQuarter = -1;
        private int _lastQuarterYear = -1;

        #endregion

        #region Fields - Level Colors

        private CrossColor _4hColor = CrossColor.FromArgb(255, 255, 193, 7); // Amber
        private CrossColor _dailyColor = CrossColor.FromArgb(255, 33, 150, 243); // Blue
        private CrossColor _mondayColor = CrossColor.FromArgb(255, 156, 39, 176); // Purple
        private CrossColor _quarterlyColor = CrossColor.FromArgb(255, 76, 175, 80); // Green

        #endregion

        #region Properties - Drawing

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

        [Display(Name = "Background Width", GroupName = "Drawing", Order = 75)]
        [Range(50, 500)]
        public int BackgroundWidth
        {
            get => _backgroundWidth;
            set
            {
                _backgroundWidth = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Use Short Labels", GroupName = "Drawing", Order = 80)]
        public bool UseShortLabels
        {
            get => _useShortLabels;
            set
            {
                _useShortLabels = value;
                RecalculateValues();
            }
        }

        #endregion

        #region Properties - Level Visibility

        [Display(Name = "Show 4H Open", GroupName = "Level Visibility - 4H", Order = 10)]
        public bool Show4hOpen
        {
            get => _show4hOpen;
            set
            {
                _show4hOpen = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show 4H High/Low", GroupName = "Level Visibility - 4H", Order = 20)]
        public bool Show4hHighLow
        {
            get => _show4hHighLow;
            set
            {
                _show4hHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Daily Open", GroupName = "Level Visibility - Daily", Order = 10)]
        public bool ShowDailyOpen
        {
            get => _showDailyOpen;
            set
            {
                _showDailyOpen = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Day High/Low", GroupName = "Level Visibility - Daily", Order = 20)]
        public bool ShowPrevDayHighLow
        {
            get => _showPrevDayHighLow;
            set
            {
                _showPrevDayHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Day Mid", GroupName = "Level Visibility - Daily", Order = 30)]
        public bool ShowPrevDayMid
        {
            get => _showPrevDayMid;
            set
            {
                _showPrevDayMid = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Monday High/Low", GroupName = "Level Visibility - Monday", Order = 10)]
        public bool ShowMondayHighLow
        {
            get => _showMondayHighLow;
            set
            {
                _showMondayHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Monday Mid", GroupName = "Level Visibility - Monday", Order = 20)]
        public bool ShowMondayMid
        {
            get => _showMondayMid;
            set
            {
                _showMondayMid = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Quarterly Open", GroupName = "Level Visibility - Quarterly", Order = 10)]
        public bool ShowQuarterlyOpen
        {
            get => _showQuarterlyOpen;
            set
            {
                _showQuarterlyOpen = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Quarter High/Low", GroupName = "Level Visibility - Quarterly", Order = 20)]
        public bool ShowPrevQuarterHighLow
        {
            get => _showPrevQuarterHighLow;
            set
            {
                _showPrevQuarterHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Quarter Mid", GroupName = "Level Visibility - Quarterly", Order = 30)]
        public bool ShowPrevQuarterMid
        {
            get => _showPrevQuarterMid;
            set
            {
                _showPrevQuarterMid = value;
                RecalculateValues();
            }
        }

        #endregion

        #region Properties - Level Colors

        [Display(Name = "4H Level Color", GroupName = "Level Colors", Order = 10)]
        public CrossColor FourHourColor
        {
            get => _4hColor;
            set
            {
                _4hColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Daily Level Color", GroupName = "Level Colors", Order = 20)]
        public CrossColor DailyColor
        {
            get => _dailyColor;
            set
            {
                _dailyColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Monday Level Color", GroupName = "Level Colors", Order = 30)]
        public CrossColor MondayColor
        {
            get => _mondayColor;
            set
            {
                _mondayColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Quarterly Level Color", GroupName = "Level Colors", Order = 40)]
        public CrossColor QuarterlyColor
        {
            get => _quarterlyColor;
            set
            {
                _quarterlyColor = value;
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

        protected override void OnRecalculate()
        {
            _current4h.Reset();
            _previous4h.Reset();
            _currentDay.Reset();
            _previousDay.Reset();
            _currentMonday.Reset();
            _previousMonday.Reset();
            _currentQuarter.Reset();
            _previousQuarter.Reset();
            _last4hPeriodStart = DateTime.MinValue;
            _lastDayStart = DateTime.MinValue;
            _lastMondayStart = DateTime.MinValue;
            _lastQuarter = -1;
            _lastQuarterYear = -1;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var candle = GetCandle(bar);

            // Process 4-hour periods
            Process4HourPeriod(bar, candle);

            // Process daily periods
            ProcessDailyPeriod(bar, candle);

            // Process Monday (weekly) periods
            ProcessMondayPeriod(bar, candle);

            // Process quarterly periods
            ProcessQuarterlyPeriod(bar, candle);
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
            int rectWidth = _backgroundWidth;
            int rectX = anchorX;

            // Adjust rectangle position based on anchor
            if (Anchor == AnchorPosition.Right)
            {
                rectX = anchorX - rectWidth;
            }

            var backgroundRect = new Rectangle(rectX, region.Top, rectWidth, region.Height);
            context.FillRectangle(BackgroundColor.Convert(), backgroundRect);

            // Collect all levels to draw
            var levels = GetDynamicLevels();

            // Draw each level
            foreach (var level in levels)
            {
                DrawLevel(context, font, level, anchorX, region);
            }

            // Check for unavailable levels and draw warning
            var unavailableLevels = GetUnavailableLevels();
            if (unavailableLevels.Count > 0)
            {
                DrawUnavailableWarning(context, font, unavailableLevels, region);
            }
        }

        #endregion

        #region Period Processing

        private void Process4HourPeriod(int bar, IndicatorCandle candle)
        {
            // Calculate the 4-hour period start time
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            var periodStart = new DateTime(
                candleTime.Year, 
                candleTime.Month, 
                candleTime.Day, 
                (candleTime.Hour / 4) * 4, 
                0, 
                0);

            if (periodStart != _last4hPeriodStart)
            {
                // New 4-hour period
                if (_current4h.IsValid)
                {
                    // Copy current to previous
                    _previous4h.Open = _current4h.Open;
                    _previous4h.High = _current4h.High;
                    _previous4h.Low = _current4h.Low;
                    _previous4h.Close = _current4h.Close;
                    _previous4h.StartTime = _current4h.StartTime;
                    _previous4h.StartBar = _current4h.StartBar;
                }

                _current4h.Initialize(candle, bar);
                _last4hPeriodStart = periodStart;
            }
            else if (_current4h.IsValid)
            {
                _current4h.Update(candle);
            }
        }

        private void ProcessDailyPeriod(int bar, IndicatorCandle candle)
        {
            if (IsNewSession(bar))
            {
                var candleTime = candle.Time;
                
                if (candleTime != _lastDayStart)
                {
                    // New day
                    if (_currentDay.IsValid)
                    {
                        // Copy current to previous
                        _previousDay.Open = _currentDay.Open;
                        _previousDay.High = _currentDay.High;
                        _previousDay.Low = _currentDay.Low;
                        _previousDay.Close = _currentDay.Close;
                        _previousDay.StartTime = _currentDay.StartTime;
                        _previousDay.StartBar = _currentDay.StartBar;
                    }

                    _currentDay.Initialize(candle, bar);
                    _lastDayStart = candleTime;
                }
            }
            else if (_currentDay.IsValid)
            {
                _currentDay.Update(candle);
            }
            else if (bar == 0)
            {
                // Initialize on first bar if no session detected yet
                _currentDay.Initialize(candle, bar);
                _lastDayStart = candle.Time;
            }
        }

        private void ProcessMondayPeriod(int bar, IndicatorCandle candle)
        {
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            var isMonday = candleTime.DayOfWeek == DayOfWeek.Monday;

            // Detect new week (Monday)
            if (IsNewWeek(bar) || (bar == 0 && isMonday))
            {
                if (candleTime.Date != _lastMondayStart.Date)
                {
                    // New Monday/week started
                    if (_currentMonday.IsValid)
                    {
                        // Copy current Monday to previous
                        _previousMonday.Open = _currentMonday.Open;
                        _previousMonday.High = _currentMonday.High;
                        _previousMonday.Low = _currentMonday.Low;
                        _previousMonday.Close = _currentMonday.Close;
                        _previousMonday.StartTime = _currentMonday.StartTime;
                        _previousMonday.StartBar = _currentMonday.StartBar;
                    }

                    _currentMonday.Initialize(candle, bar);
                    _lastMondayStart = candleTime;
                }
            }
            else if (isMonday && _currentMonday.IsValid && _currentMonday.StartTime.Date == candleTime.Date)
            {
                // Still the same Monday, update the range
                _currentMonday.Update(candle);
            }
        }

        private void ProcessQuarterlyPeriod(int bar, IndicatorCandle candle)
        {
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            var currentQuarter = (candleTime.Month - 1) / 3 + 1; // Q1=1, Q2=2, Q3=3, Q4=4
            var currentYear = candleTime.Year;

            // Detect new quarter
            if (currentQuarter != _lastQuarter || currentYear != _lastQuarterYear)
            {
                if (_lastQuarter != -1) // Not the first time
                {
                    if (_currentQuarter.IsValid)
                    {
                        // Copy current to previous
                        _previousQuarter.Open = _currentQuarter.Open;
                        _previousQuarter.High = _currentQuarter.High;
                        _previousQuarter.Low = _currentQuarter.Low;
                        _previousQuarter.Close = _currentQuarter.Close;
                        _previousQuarter.StartTime = _currentQuarter.StartTime;
                        _previousQuarter.StartBar = _currentQuarter.StartBar;
                    }
                }

                _currentQuarter.Initialize(candle, bar);
                _lastQuarter = currentQuarter;
                _lastQuarterYear = currentYear;
            }
            else if (_currentQuarter.IsValid)
            {
                _currentQuarter.Update(candle);
            }
        }

        #endregion

        #region Level Collection

        private List<KeyLevel> GetDynamicLevels()
        {
            var levels = new List<KeyLevel>();

            // Previous 4H High/Low
            if (_show4hHighLow && _previous4h.IsValid)
            {
                levels.Add(new KeyLevel(_previous4h.High, _useShortLabels ? "P4HH" : "Prev 4H High", _4hColor));
                levels.Add(new KeyLevel(_previous4h.Low, _useShortLabels ? "P4HL" : "Prev 4H Low", _4hColor));
            }

            // Current 4H Open
            if (_show4hOpen && _current4h.IsValid)
            {
                levels.Add(new KeyLevel(_current4h.Open, _useShortLabels ? "4HO" : "4H Open", _4hColor));
            }

            // Daily Open
            if (_showDailyOpen && _currentDay.IsValid)
            {
                levels.Add(new KeyLevel(_currentDay.Open, _useShortLabels ? "DO" : "Day Open", _dailyColor));
            }

            // Previous Day High/Low
            if (_showPrevDayHighLow && _previousDay.IsValid)
            {
                levels.Add(new KeyLevel(_previousDay.High, _useShortLabels ? "PDH" : "Prev Day High", _dailyColor));
                levels.Add(new KeyLevel(_previousDay.Low, _useShortLabels ? "PDL" : "Prev Day Low", _dailyColor));
            }

            // Previous Day Mid
            if (_showPrevDayMid && _previousDay.IsValid)
            {
                levels.Add(new KeyLevel(_previousDay.Mid, _useShortLabels ? "PDM" : "Prev Day Mid", _dailyColor));
            }

            // Monday High/Low - prefer current week's Monday, fallback to previous
            var mondayRange = _currentMonday.IsValid ? _currentMonday : _previousMonday;
            if (_showMondayHighLow && mondayRange.IsValid)
            {
                var isPrev = mondayRange == _previousMonday;
                levels.Add(new KeyLevel(mondayRange.High, _useShortLabels ? (isPrev ? "PMDAYH" : "MDAYH") : (isPrev ? "Prev Mon High" : "Mon High"), _mondayColor));
                levels.Add(new KeyLevel(mondayRange.Low, _useShortLabels ? (isPrev ? "PMDAYL" : "MDAYL") : (isPrev ? "Prev Mon Low" : "Mon Low"), _mondayColor));
            }

            // Monday Mid
            if (_showMondayMid && mondayRange.IsValid)
            {
                var isPrev = mondayRange == _previousMonday;
                levels.Add(new KeyLevel(mondayRange.Mid, _useShortLabels ? (isPrev ? "PMDAYM" : "MDAYM") : (isPrev ? "Prev Mon Mid" : "Mon Mid"), _mondayColor));
            }

            // Quarterly Open
            if (_showQuarterlyOpen && _currentQuarter.IsValid)
            {
                levels.Add(new KeyLevel(_currentQuarter.Open, _useShortLabels ? "QO" : "Quarter Open", _quarterlyColor));
            }

            // Previous Quarter High/Low
            if (_showPrevQuarterHighLow && _previousQuarter.IsValid)
            {
                levels.Add(new KeyLevel(_previousQuarter.High, _useShortLabels ? "PQH" : "Prev Quarter High", _quarterlyColor));
                levels.Add(new KeyLevel(_previousQuarter.Low, _useShortLabels ? "PQL" : "Prev Quarter Low", _quarterlyColor));
            }

            // Previous Quarter Mid
            if (_showPrevQuarterMid && _previousQuarter.IsValid)
            {
                levels.Add(new KeyLevel(_previousQuarter.Mid, _useShortLabels ? "PQM" : "Prev Quarter Mid", _quarterlyColor));
            }

            return levels;
        }

        private List<string> GetUnavailableLevels()
        {
            var unavailable = new List<string>();

            // 4H checks
            if (_show4hOpen && !_current4h.IsValid)
                unavailable.Add("4H Open");
            if (_show4hHighLow && !_previous4h.IsValid)
                unavailable.Add("4H H/L");

            // Daily checks
            if (_showDailyOpen && !_currentDay.IsValid)
                unavailable.Add("Day Open");
            if (_showPrevDayHighLow && !_previousDay.IsValid)
                unavailable.Add("PD H/L");
            if (_showPrevDayMid && !_previousDay.IsValid)
                unavailable.Add("PD Mid");

            // Monday checks
            var mondayRange = _currentMonday.IsValid ? _currentMonday : _previousMonday;
            if (_showMondayHighLow && !mondayRange.IsValid)
                unavailable.Add("Mon H/L");
            if (_showMondayMid && !mondayRange.IsValid)
                unavailable.Add("Mon Mid");

            // Quarterly checks
            if (_showQuarterlyOpen && !_currentQuarter.IsValid)
                unavailable.Add("Q Open");
            if (_showPrevQuarterHighLow && !_previousQuarter.IsValid)
                unavailable.Add("PQ H/L");
            if (_showPrevQuarterMid && !_previousQuarter.IsValid)
                unavailable.Add("PQ Mid");

            return unavailable;
        }

        #endregion

        #region Drawing Methods

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
            if (level.Price == 0)
                return;

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

            // Draw the horizontal line at the price level using the level's color
            var linePen = new RenderPen(level.Color.Convert(), 2);
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

        private void DrawUnavailableWarning(RenderContext context, RenderFont font, List<string> unavailableLevels, Rectangle region)
        {
            // Build short warning message
            var message = "N/A: " + string.Join(", ", unavailableLevels);

            // Measure text size
            var textSize = context.MeasureString(message, font);

            // Calculate rectangle with padding
            const int paddingX = 10;
            const int paddingY = 5;
            const int topMargin = 10;

            int rectWidth = textSize.Width + (paddingX * 2);
            int rectHeight = textSize.Height + (paddingY * 2);
            int rectX = region.Left + (region.Width - rectWidth) / 2;
            int rectY = region.Top + topMargin;

            // Draw dark red background rectangle
            var errorBackgroundColor = Color.FromArgb(200, 139, 0, 0); // Dark red with some transparency
            var errorRect = new Rectangle(rectX, rectY, rectWidth, rectHeight);
            context.FillRectangle(errorBackgroundColor, errorRect);

            // Draw white text centered in rectangle
            var textRect = new Rectangle(
                rectX + paddingX,
                rectY + paddingY,
                textSize.Width,
                textSize.Height);

            context.DrawString(message, font, Color.White, textRect, _labelFormat);
        }

        #endregion
    }
}
