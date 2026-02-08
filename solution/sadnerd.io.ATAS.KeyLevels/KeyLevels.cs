using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using sadnerd.io.ATAS.KeyLevels.DataAggregation;
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
    /// Represents a level with its calculated label position (after overlap resolution)
    /// </summary>
    internal class LabelPosition
    {
        public KeyLevel Level { get; set; }
        public int LevelY { get; set; }      // Actual price level Y coordinate
        public int LabelY { get; set; }      // Label Y coordinate (may be offset)
        public int LabelHeight { get; set; } // Height of the label text

        public LabelPosition(KeyLevel level, int levelY, int labelHeight)
        {
            Level = level;
            LevelY = levelY;
            LabelY = levelY; // Initially same as level Y
            LabelHeight = labelHeight;
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
        private bool _show4hMid = true;

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

        // Yearly visibility
        private bool _showPrevYearHighLow = true;
        private bool _showPrevYearMid = true;
        private bool _showCurrentYearHighLow = true;
        private bool _showCurrentYearMid = true;

        // Weekly visibility (full week)
        private bool _showWeekOpen = true;
        private bool _showPrevWeekHighLow = true;
        private bool _showPrevWeekMid = true;

        // Monthly visibility
        private bool _showMonthOpen = true;
        private bool _showPrevMonthHighLow = true;
        private bool _showPrevMonthMid = true;

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
        private DateTime _sessionStartTime; // Track session start for 4H alignment

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

        // Yearly
        private readonly PeriodRange _currentYear = new();
        private readonly PeriodRange _previousYear = new();
        private int _lastYear = -1;

        // Weekly (full week)
        private readonly PeriodRange _currentWeek = new();
        private readonly PeriodRange _previousWeek = new();
        private DateTime _lastWeekStart;

        // Monthly
        private readonly PeriodRange _currentMonth = new();
        private readonly PeriodRange _previousMonth = new();
        private int _lastMonth = -1;
        private int _lastMonthYear = -1;

        #endregion

        #region Fields - Data Aggregation

        private InstrumentDataStore? _dataStore;
        private string _contributorId = Guid.NewGuid().ToString();
        private DateTime _lastContributionTime = DateTime.MinValue;

        #endregion

        #region Fields - Level Colors

        private CrossColor _4hColor = CrossColor.FromArgb(255, 255, 193, 7); // Amber
        private CrossColor _dailyColor = CrossColor.FromArgb(255, 33, 150, 243); // Blue
        private CrossColor _mondayColor = CrossColor.FromArgb(255, 156, 39, 176); // Purple
        private CrossColor _quarterlyColor = CrossColor.FromArgb(255, 76, 175, 80); // Green
        private CrossColor _yearlyColor = CrossColor.FromArgb(255, 0, 150, 136); // Teal
        private CrossColor _weeklyColor = CrossColor.FromArgb(255, 255, 152, 0); // Orange
        private CrossColor _monthlyColor = CrossColor.FromArgb(255, 0, 188, 212); // Cyan

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

        [Display(Name = "Show 4H Mid", GroupName = "Level Visibility - 4H", Order = 30)]
        public bool Show4hMid
        {
            get => _show4hMid;
            set
            {
                _show4hMid = value;
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

        // Yearly visibility properties
        [Display(Name = "Show Prev Year High/Low", GroupName = "Level Visibility - Yearly", Order = 10)]
        public bool ShowPrevYearHighLow
        {
            get => _showPrevYearHighLow;
            set
            {
                _showPrevYearHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Year Mid", GroupName = "Level Visibility - Yearly", Order = 20)]
        public bool ShowPrevYearMid
        {
            get => _showPrevYearMid;
            set
            {
                _showPrevYearMid = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Current Year High/Low", GroupName = "Level Visibility - Yearly", Order = 30)]
        public bool ShowCurrentYearHighLow
        {
            get => _showCurrentYearHighLow;
            set
            {
                _showCurrentYearHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Current Year Mid", GroupName = "Level Visibility - Yearly", Order = 40)]
        public bool ShowCurrentYearMid
        {
            get => _showCurrentYearMid;
            set
            {
                _showCurrentYearMid = value;
                RecalculateValues();
            }
        }

        // Weekly visibility properties (full week)
        [Display(Name = "Show Week Open", GroupName = "Level Visibility - Weekly", Order = 10)]
        public bool ShowWeekOpen
        {
            get => _showWeekOpen;
            set
            {
                _showWeekOpen = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Week High/Low", GroupName = "Level Visibility - Weekly", Order = 20)]
        public bool ShowPrevWeekHighLow
        {
            get => _showPrevWeekHighLow;
            set
            {
                _showPrevWeekHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Week Mid", GroupName = "Level Visibility - Weekly", Order = 30)]
        public bool ShowPrevWeekMid
        {
            get => _showPrevWeekMid;
            set
            {
                _showPrevWeekMid = value;
                RecalculateValues();
            }
        }

        // Monthly visibility properties
        [Display(Name = "Show Month Open", GroupName = "Level Visibility - Monthly", Order = 10)]
        public bool ShowMonthOpen
        {
            get => _showMonthOpen;
            set
            {
                _showMonthOpen = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Month High/Low", GroupName = "Level Visibility - Monthly", Order = 20)]
        public bool ShowPrevMonthHighLow
        {
            get => _showPrevMonthHighLow;
            set
            {
                _showPrevMonthHighLow = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show Prev Month Mid", GroupName = "Level Visibility - Monthly", Order = 30)]
        public bool ShowPrevMonthMid
        {
            get => _showPrevMonthMid;
            set
            {
                _showPrevMonthMid = value;
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

        [Display(Name = "Yearly Level Color", GroupName = "Level Colors", Order = 50)]
        public CrossColor YearlyColor
        {
            get => _yearlyColor;
            set
            {
                _yearlyColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Weekly Level Color", GroupName = "Level Colors", Order = 60)]
        public CrossColor WeeklyColor
        {
            get => _weeklyColor;
            set
            {
                _weeklyColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Monthly Level Color", GroupName = "Level Colors", Order = 70)]
        public CrossColor MonthlyColor
        {
            get => _monthlyColor;
            set
            {
                _monthlyColor = value;
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

        #region Data Aggregation

        /// <summary>
        /// Initializes the data store for the current instrument.
        /// </summary>
        private void EnsureDataStoreInitialized()
        {
            if (_dataStore != null)
                return;

            var instrument = InstrumentInfo?.Instrument;
            if (!string.IsNullOrEmpty(instrument))
            {
                _dataStore = KeyLevelDataService.Instance.GetStore(instrument);
            }
        }

        /// <summary>
        /// Contributes current period data to the aggregation layer.
        /// </summary>
        private void ContributeDataToAggregator(IndicatorCandle latestCandle)
        {
            if (_dataStore == null)
                return;

            var now = latestCandle.Time;
            
            // Contribute Daily data
            if (_currentDay.IsValid)
            {
                ContributePeriod(_currentDay, PeriodType.Daily, true, _lastDayStart, GetDayEnd(_lastDayStart));
            }
            if (_previousDay.IsValid)
            {
                ContributePeriod(_previousDay, PeriodType.Daily, false, _previousDay.StartTime, _lastDayStart);
            }

            // Contribute 4H data
            if (_current4h.IsValid)
            {
                ContributePeriod(_current4h, PeriodType.FourHour, true, _last4hPeriodStart, _last4hPeriodStart.AddHours(4));
            }
            if (_previous4h.IsValid)
            {
                ContributePeriod(_previous4h, PeriodType.FourHour, false, _previous4h.StartTime, _last4hPeriodStart);
            }

            // Contribute Weekly data
            if (_currentWeek.IsValid)
            {
                ContributePeriod(_currentWeek, PeriodType.Weekly, true, _lastWeekStart, _lastWeekStart.AddDays(7));
            }
            if (_previousWeek.IsValid)
            {
                ContributePeriod(_previousWeek, PeriodType.Weekly, false, _previousWeek.StartTime, _lastWeekStart);
            }

            // Contribute Monday data
            if (_currentMonday.IsValid)
            {
                ContributePeriod(_currentMonday, PeriodType.Monday, true, _lastMondayStart, _lastMondayStart.AddDays(1));
            }
            if (_previousMonday.IsValid)
            {
                ContributePeriod(_previousMonday, PeriodType.Monday, false, _previousMonday.StartTime, _lastMondayStart);
            }

            // Contribute Monthly data
            if (_currentMonth.IsValid)
            {
                var monthStart = new DateTime(_lastMonthYear, _lastMonth, 1);
                ContributePeriod(_currentMonth, PeriodType.Monthly, true, monthStart, monthStart.AddMonths(1));
            }
            if (_previousMonth.IsValid)
            {
                var prevMonthStart = _previousMonth.StartTime;
                var currentMonthStart = new DateTime(_lastMonthYear, _lastMonth, 1);
                ContributePeriod(_previousMonth, PeriodType.Monthly, false, prevMonthStart, currentMonthStart);
            }

            // Contribute Quarterly data
            if (_currentQuarter.IsValid)
            {
                var quarterStart = GetQuarterStart(_lastQuarterYear, _lastQuarter);
                ContributePeriod(_currentQuarter, PeriodType.Quarterly, true, quarterStart, quarterStart.AddMonths(3));
            }
            if (_previousQuarter.IsValid)
            {
                var prevQuarterStart = _previousQuarter.StartTime;
                var currentQuarterStart = GetQuarterStart(_lastQuarterYear, _lastQuarter);
                ContributePeriod(_previousQuarter, PeriodType.Quarterly, false, prevQuarterStart, currentQuarterStart);
            }

            // Contribute Yearly data
            if (_currentYear.IsValid)
            {
                var yearStart = new DateTime(_lastYear, 1, 1);
                ContributePeriod(_currentYear, PeriodType.Yearly, true, yearStart, yearStart.AddYears(1));
            }
            if (_previousYear.IsValid)
            {
                var prevYearStart = new DateTime(_lastYear - 1, 1, 1);
                var currentYearStart = new DateTime(_lastYear, 1, 1);
                ContributePeriod(_previousYear, PeriodType.Yearly, false, prevYearStart, currentYearStart);
            }
        }

        private void ContributePeriod(PeriodRange range, PeriodType periodType, bool isCurrent, DateTime periodStart, DateTime periodEnd)
        {
            if (_dataStore == null || !range.IsValid)
                return;

            var timeRange = new TimeRange
            {
                Start = range.StartTime,
                End = GetCandle(CurrentBar - 1).Time.AddMinutes(GetCandleDurationMinutes()),
                Open = range.Open,
                High = range.High,
                Low = range.Low,
                Close = range.Close,
                SourceId = _contributorId
            };

            _dataStore.ContributePeriodData(periodType, isCurrent, periodStart, periodEnd, timeRange);
        }

        private DateTime GetDayEnd(DateTime dayStart)
        {
            // TODO: Ideally use session end time, for now assume next day
            return dayStart.Date.AddDays(1);
        }

        private DateTime GetQuarterStart(int year, int quarter)
        {
            var month = (quarter - 1) * 3 + 1;
            return new DateTime(year, month, 1);
        }

        private int GetCandleDurationMinutes()
        {
            // Estimate candle duration from context
            if (CurrentBar < 2)
                return 1;

            var candle0 = GetCandle(0);
            var candle1 = GetCandle(1);
            return (int)Math.Max(1, (candle1.Time - candle0.Time).TotalMinutes);
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
            _currentYear.Reset();
            _previousYear.Reset();
            _currentWeek.Reset();
            _previousWeek.Reset();
            _currentMonth.Reset();
            _previousMonth.Reset();
            _last4hPeriodStart = DateTime.MinValue;
            _sessionStartTime = DateTime.MinValue;
            _lastDayStart = DateTime.MinValue;
            _lastMondayStart = DateTime.MinValue;
            _lastQuarter = -1;
            _lastQuarterYear = -1;
            _lastYear = -1;
            _lastWeekStart = DateTime.MinValue;
            _lastMonth = -1;
            _lastMonthYear = -1;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            // Ensure data store is initialized for this instrument
            EnsureDataStoreInitialized();

            var candle = GetCandle(bar);

            // Process daily periods first (need session start for 4H calculation)
            ProcessDailyPeriod(bar, candle);

            // Process 4-hour periods (depends on daily session start)
            Process4HourPeriod(bar, candle);

            // Process Monday (weekly) periods
            ProcessMondayPeriod(bar, candle);

            // Process quarterly periods
            ProcessQuarterlyPeriod(bar, candle);

            // Process yearly periods
            ProcessYearlyPeriod(bar, candle);

            // Process weekly (full week) periods
            ProcessWeeklyPeriod(bar, candle);

            // Process monthly periods
            ProcessMonthlyPeriod(bar, candle);

            // Contribute processed data to the aggregation layer
            ContributeDataToAggregator(candle);
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

            // Calculate label positions with overlap prevention
            var labelPositions = CalculateLabelPositions(context, font, levels, region);

            // Draw each level with its calculated label position
            foreach (var pos in labelPositions)
            {
                DrawLevelWithBranch(context, font, pos, anchorX, region);
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
            // Need a valid session start time to calculate 4H periods
            if (_sessionStartTime == DateTime.MinValue)
                return;

            var candleTime = candle.Time;

            // Calculate hours since session start
            var hoursSinceSessionStart = (candleTime - _sessionStartTime).TotalHours;
            
            // Determine which 4H period this candle belongs to (0 = first 4H, 1 = second 4H, etc.)
            var periodIndex = (int)Math.Floor(hoursSinceSessionStart / 4);
            var periodStart = _sessionStartTime.AddHours(periodIndex * 4);

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
                    
                    // Update session start time for 4H calculation
                    // Previous 4H is preserved across sessions (last 4H of previous session)
                    _sessionStartTime = candleTime;
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
                _sessionStartTime = candle.Time;
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

        private void ProcessYearlyPeriod(int bar, IndicatorCandle candle)
        {
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            var currentYear = candleTime.Year;

            // Detect new year
            if (currentYear != _lastYear)
            {
                if (_lastYear != -1) // Not the first time
                {
                    if (_currentYear.IsValid)
                    {
                        // Copy current to previous
                        _previousYear.Open = _currentYear.Open;
                        _previousYear.High = _currentYear.High;
                        _previousYear.Low = _currentYear.Low;
                        _previousYear.Close = _currentYear.Close;
                        _previousYear.StartTime = _currentYear.StartTime;
                        _previousYear.StartBar = _currentYear.StartBar;
                    }
                }

                _currentYear.Initialize(candle, bar);
                _lastYear = currentYear;
            }
            else if (_currentYear.IsValid)
            {
                _currentYear.Update(candle);
            }
        }

        private void ProcessWeeklyPeriod(int bar, IndicatorCandle candle)
        {
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            
            // Calculate the Monday of the current week
            var daysSinceMonday = (int)candleTime.DayOfWeek - 1;
            if (daysSinceMonday < 0) daysSinceMonday = 6; // Sunday
            var weekStart = candleTime.Date.AddDays(-daysSinceMonday);

            // Detect new week
            if (weekStart != _lastWeekStart)
            {
                if (_lastWeekStart != DateTime.MinValue) // Not the first time
                {
                    if (_currentWeek.IsValid)
                    {
                        // Copy current to previous
                        _previousWeek.Open = _currentWeek.Open;
                        _previousWeek.High = _currentWeek.High;
                        _previousWeek.Low = _currentWeek.Low;
                        _previousWeek.Close = _currentWeek.Close;
                        _previousWeek.StartTime = _currentWeek.StartTime;
                        _previousWeek.StartBar = _currentWeek.StartBar;
                    }
                }

                _currentWeek.Initialize(candle, bar);
                _lastWeekStart = weekStart;
            }
            else if (_currentWeek.IsValid)
            {
                _currentWeek.Update(candle);
            }
        }

        private void ProcessMonthlyPeriod(int bar, IndicatorCandle candle)
        {
            var candleTime = candle.Time.AddHours(InstrumentInfo?.TimeZone ?? 0);
            var currentMonth = candleTime.Month;
            var currentYear = candleTime.Year;

            // Detect new month
            if (currentMonth != _lastMonth || currentYear != _lastMonthYear)
            {
                if (_lastMonth != -1) // Not the first time
                {
                    if (_currentMonth.IsValid)
                    {
                        // Copy current to previous
                        _previousMonth.Open = _currentMonth.Open;
                        _previousMonth.High = _currentMonth.High;
                        _previousMonth.Low = _currentMonth.Low;
                        _previousMonth.Close = _currentMonth.Close;
                        _previousMonth.StartTime = _currentMonth.StartTime;
                        _previousMonth.StartBar = _currentMonth.StartBar;
                    }
                }

                _currentMonth.Initialize(candle, bar);
                _lastMonth = currentMonth;
                _lastMonthYear = currentYear;
            }
            else if (_currentMonth.IsValid)
            {
                _currentMonth.Update(candle);
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

            // Previous 4H Mid
            if (_show4hMid && _previous4h.IsValid)
            {
                levels.Add(new KeyLevel(_previous4h.Mid, _useShortLabels ? "P4HM" : "Prev 4H Mid", _4hColor));
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

            // Previous Year High/Low
            if (_showPrevYearHighLow && _previousYear.IsValid)
            {
                levels.Add(new KeyLevel(_previousYear.High, _useShortLabels ? "PYH" : "Prev Year High", _yearlyColor));
                levels.Add(new KeyLevel(_previousYear.Low, _useShortLabels ? "PYL" : "Prev Year Low", _yearlyColor));
            }

            // Previous Year Mid
            if (_showPrevYearMid && _previousYear.IsValid)
            {
                levels.Add(new KeyLevel(_previousYear.Mid, _useShortLabels ? "PYM" : "Prev Year Mid", _yearlyColor));
            }

            // Current Year High/Low
            if (_showCurrentYearHighLow && _currentYear.IsValid)
            {
                levels.Add(new KeyLevel(_currentYear.High, _useShortLabels ? "CYH" : "Year High", _yearlyColor));
                levels.Add(new KeyLevel(_currentYear.Low, _useShortLabels ? "CYL" : "Year Low", _yearlyColor));
            }

            // Current Year Mid
            if (_showCurrentYearMid && _currentYear.IsValid)
            {
                levels.Add(new KeyLevel(_currentYear.Mid, _useShortLabels ? "CYM" : "Year Mid", _yearlyColor));
            }

            // Week Open
            if (_showWeekOpen && _currentWeek.IsValid)
            {
                levels.Add(new KeyLevel(_currentWeek.Open, _useShortLabels ? "WO" : "Week Open", _weeklyColor));
            }

            // Previous Week High/Low
            if (_showPrevWeekHighLow && _previousWeek.IsValid)
            {
                levels.Add(new KeyLevel(_previousWeek.High, _useShortLabels ? "PWH" : "Prev Week High", _weeklyColor));
                levels.Add(new KeyLevel(_previousWeek.Low, _useShortLabels ? "PWL" : "Prev Week Low", _weeklyColor));
            }

            // Previous Week Mid
            if (_showPrevWeekMid && _previousWeek.IsValid)
            {
                levels.Add(new KeyLevel(_previousWeek.Mid, _useShortLabels ? "PWM" : "Prev Week Mid", _weeklyColor));
            }

            // Month Open
            if (_showMonthOpen && _currentMonth.IsValid)
            {
                levels.Add(new KeyLevel(_currentMonth.Open, _useShortLabels ? "MO" : "Month Open", _monthlyColor));
            }

            // Previous Month High/Low
            if (_showPrevMonthHighLow && _previousMonth.IsValid)
            {
                levels.Add(new KeyLevel(_previousMonth.High, _useShortLabels ? "PMH" : "Prev Month High", _monthlyColor));
                levels.Add(new KeyLevel(_previousMonth.Low, _useShortLabels ? "PML" : "Prev Month Low", _monthlyColor));
            }

            // Previous Month Mid
            if (_showPrevMonthMid && _previousMonth.IsValid)
            {
                levels.Add(new KeyLevel(_previousMonth.Mid, _useShortLabels ? "PMM" : "Prev Month Mid", _monthlyColor));
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
            if (_show4hMid && !_previous4h.IsValid)
                unavailable.Add("4H Mid");

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

            // Yearly checks
            if (_showPrevYearHighLow && !_previousYear.IsValid)
                unavailable.Add("PY H/L");
            if (_showPrevYearMid && !_previousYear.IsValid)
                unavailable.Add("PY Mid");
            if (_showCurrentYearHighLow && !_currentYear.IsValid)
                unavailable.Add("CY H/L");
            if (_showCurrentYearMid && !_currentYear.IsValid)
                unavailable.Add("CY Mid");

            // Weekly checks (full week)
            if (_showWeekOpen && !_currentWeek.IsValid)
                unavailable.Add("W Open");
            if (_showPrevWeekHighLow && !_previousWeek.IsValid)
                unavailable.Add("PW H/L");
            if (_showPrevWeekMid && !_previousWeek.IsValid)
                unavailable.Add("PW Mid");

            // Monthly checks
            if (_showMonthOpen && !_currentMonth.IsValid)
                unavailable.Add("M Open");
            if (_showPrevMonthHighLow && !_previousMonth.IsValid)
                unavailable.Add("PM H/L");
            if (_showPrevMonthMid && !_previousMonth.IsValid)
                unavailable.Add("PM Mid");

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

            // For Right anchor, positive distance should move left (towards chart interior)
            int effectiveDistance = Anchor == AnchorPosition.Right 
                ? -DistanceFromAnchor 
                : DistanceFromAnchor;

            return baseX + effectiveDistance;
        }

        private List<LabelPosition> CalculateLabelPositions(RenderContext context, RenderFont font, List<KeyLevel> levels, Rectangle region)
        {
            var positions = new List<LabelPosition>();

            // First pass: create positions for all visible levels
            foreach (var level in levels)
            {
                if (level.Price == 0)
                    continue;

                int y = ChartInfo.GetYByPrice(level.Price, false);

                // Skip if not visible
                if (y < region.Top || y > region.Bottom)
                    continue;

                var textSize = context.MeasureString(level.Label, font);
                positions.Add(new LabelPosition(level, y, textSize.Height));
            }

            // Sort by original Y position (top to bottom) - this order is STABLE
            positions.Sort((a, b) => a.LevelY.CompareTo(b.LevelY));

            if (positions.Count < 2)
                return positions;

            const int minSpacing = 2;
            const int maxIterations = 20;

            // Global optimization using iterative relaxation
            // Goal: minimize sum of |LabelY - LevelY| while ensuring no overlaps
            
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changed = false;

                // For each adjacent pair, if overlapping, split the required shift
                for (int i = 0; i < positions.Count - 1; i++)
                {
                    var upper = positions[i];
                    var lower = positions[i + 1];

                    int upperBottom = upper.LabelY + upper.LabelHeight / 2;
                    int lowerTop = lower.LabelY - lower.LabelHeight / 2;
                    int overlap = upperBottom + minSpacing - lowerTop;

                    if (overlap > 0)
                    {
                        changed = true;

                        // Calculate how much room each label has to move
                        int upperRoom = CalculateUpwardRoom(positions, i, minSpacing);
                        int lowerRoom = CalculateDownwardRoom(positions, i + 1, minSpacing);

                        // Distribute the shift proportionally based on available room
                        int totalRoom = upperRoom + lowerRoom;
                        if (totalRoom > 0)
                        {
                            int upperShift = (overlap * upperRoom) / totalRoom;
                            int lowerShift = overlap - upperShift;

                            // Apply shifts (upper goes up, lower goes down)
                            ShiftLabelAndNeighbors(positions, i, -upperShift, minSpacing, true);
                            ShiftLabelAndNeighbors(positions, i + 1, lowerShift, minSpacing, false);
                        }
                        else
                        {
                            // No room - split evenly
                            int halfShift = overlap / 2;
                            ShiftLabelAndNeighbors(positions, i, -halfShift, minSpacing, true);
                            ShiftLabelAndNeighbors(positions, i + 1, overlap - halfShift, minSpacing, false);
                        }
                    }
                }

                if (!changed)
                    break;
            }

            // Final pass: ensure no overlaps (safety net)
            for (int i = 1; i < positions.Count; i++)
            {
                var prev = positions[i - 1];
                var curr = positions[i];

                int minY = prev.LabelY + prev.LabelHeight / 2 + minSpacing + curr.LabelHeight / 2;
                if (curr.LabelY < minY)
                {
                    curr.LabelY = minY;
                }
            }

            return positions;
        }

        private int CalculateUpwardRoom(List<LabelPosition> positions, int index, int minSpacing)
        {
            var pos = positions[index];
            
            // Room is limited by: original position (don't go above it too much) and previous label
            int minAllowedY = pos.LevelY - pos.LabelHeight * 2; // Allow some movement above original
            
            if (index > 0)
            {
                var prev = positions[index - 1];
                int prevBottom = prev.LabelY + prev.LabelHeight / 2 + minSpacing + pos.LabelHeight / 2;
                minAllowedY = Math.Max(minAllowedY, prevBottom);
            }

            return Math.Max(0, pos.LabelY - minAllowedY);
        }

        private int CalculateDownwardRoom(List<LabelPosition> positions, int index, int minSpacing)
        {
            var pos = positions[index];
            
            // Room is limited by: original position (don't go below it too much) and next label
            int maxAllowedY = pos.LevelY + pos.LabelHeight * 2; // Allow some movement below original
            
            if (index < positions.Count - 1)
            {
                var next = positions[index + 1];
                int nextTop = next.LabelY - next.LabelHeight / 2 - minSpacing - pos.LabelHeight / 2;
                maxAllowedY = Math.Min(maxAllowedY, nextTop);
            }

            return Math.Max(0, maxAllowedY - pos.LabelY);
        }

        private void ShiftLabelAndNeighbors(List<LabelPosition> positions, int index, int shift, int minSpacing, bool goingUp)
        {
            if (shift == 0) return;

            positions[index].LabelY += shift;

            // Propagate shift to neighbors if needed
            if (goingUp && index > 0)
            {
                // Check if we now overlap with the label above
                var curr = positions[index];
                var prev = positions[index - 1];
                
                int prevBottom = prev.LabelY + prev.LabelHeight / 2;
                int currTop = curr.LabelY - curr.LabelHeight / 2;
                
                if (currTop < prevBottom + minSpacing)
                {
                    int neededShift = currTop - prevBottom - minSpacing;
                    ShiftLabelAndNeighbors(positions, index - 1, neededShift, minSpacing, true);
                }
            }
            else if (!goingUp && index < positions.Count - 1)
            {
                // Check if we now overlap with the label below
                var curr = positions[index];
                var next = positions[index + 1];
                
                int currBottom = curr.LabelY + curr.LabelHeight / 2;
                int nextTop = next.LabelY - next.LabelHeight / 2;
                
                if (currBottom + minSpacing > nextTop)
                {
                    int neededShift = currBottom + minSpacing - nextTop;
                    ShiftLabelAndNeighbors(positions, index + 1, neededShift, minSpacing, false);
                }
            }
        }

        private void DrawLevelWithBranch(RenderContext context, RenderFont font, LabelPosition pos, int anchorX, Rectangle region)
        {
            var level = pos.Level;
            int levelY = pos.LevelY;
            int labelY = pos.LabelY;

            // Calculate line positions
            int lineStartX;
            int lineEndX;
            int textX;
            int branchDirection; // 1 = right, -1 = left

            if (Anchor == AnchorPosition.Right)
            {
                lineEndX = anchorX;
                lineStartX = anchorX - _lineWidth;
                textX = lineStartX - 5;
                branchDirection = -1;
            }
            else
            {
                lineStartX = anchorX;
                lineEndX = anchorX + _lineWidth;
                textX = lineEndX + 5;
                branchDirection = 1;
            }

            var linePen = new RenderPen(level.Color.Convert(), 2);

            // Draw the horizontal line at the actual price level
            context.DrawLine(linePen, lineStartX, levelY, lineEndX, levelY);

            // If label is offset, draw diagonal branch line
            int yOffset = labelY - levelY;
            if (Math.Abs(yOffset) > 2)
            {
                // 45-degree diagonal: horizontal distance = vertical distance
                int diagonalLength = Math.Abs(yOffset);
                
                int branchStartX = (Anchor == AnchorPosition.Right) ? lineStartX : lineEndX;
                int branchEndX = branchStartX + (diagonalLength * branchDirection);

                // Draw diagonal line from level to offset position
                context.DrawLine(linePen, branchStartX, levelY, branchEndX, labelY);

                // Update text position to be after the diagonal
                if (Anchor == AnchorPosition.Right)
                {
                    textX = branchEndX - 5;
                }
                else
                {
                    textX = branchEndX + 5;
                }
            }

            // Draw the text label
            var textSize = context.MeasureString(level.Label, font);

            Rectangle textRect;
            if (Anchor == AnchorPosition.Right)
            {
                textRect = new Rectangle(textX - textSize.Width, labelY - textSize.Height / 2, textSize.Width, textSize.Height);
            }
            else
            {
                textRect = new Rectangle(textX, labelY - textSize.Height / 2, textSize.Width, textSize.Height);
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
