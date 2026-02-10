using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ATAS.Indicators;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using sadnerd.io.ATAS.KeyLevels.DataAggregation;
using sadnerd.io.ATAS.KeyLevels.DataStore;
using Utils.Common.Logging;
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
    /// Date format for displaying dates next to level labels
    /// </summary>
    public enum LevelDateFormat
    {
        Off,
        MonthDay,  // MM/dd
        DayMonth   // dd/MM
    }

    /// <summary>
    /// Represents a key price level with a label and color
    /// </summary>
    public class KeyLevel
    {
        public decimal Price { get; set; }
        public string Label { get; set; }
        public CrossColor Color { get; set; }
        public DateTime? Date { get; set; }  // Optional date for daily and smaller timeframes
        public int? StartBar { get; set; }   // Bar where this level originated (for ray rendering)

        public KeyLevel(decimal price, string label, CrossColor color, DateTime? date = null, int? startBar = null)
        {
            Price = price;
            Label = label;
            Color = color;
            Date = date;
            StartBar = startBar;
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
        private LevelDateFormat _levelDateFormat = LevelDateFormat.Off;
        private bool _renderAsRays = false;

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

        // Time-based period store for O(1) ingest
        private TimeBasedPeriodStore? _periodStore;

        #endregion

        #region Fields - Data Aggregation

        private InstrumentDataStore? _dataStore;
        private string _contributorId = Guid.NewGuid().ToString();
        private DateTime _lastContributionTime = DateTime.MinValue;
        private DateTime _lastDiagnosticLogTime = DateTime.MinValue;
        private const int DiagnosticLogIntervalSeconds = 15;

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

        [Display(Name = "Show Date on Labels", GroupName = "Drawing", Order = 85)]
        public LevelDateFormat LevelDateFormat
        {
            get => _levelDateFormat;
            set
            {
                _levelDateFormat = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Render as Rays", GroupName = "Drawing", Order = 90)]
        public bool RenderAsRays
        {
            get => _renderAsRays;
            set
            {
                _renderAsRays = value;
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

        private void ContributeDataToAggregator(IndicatorCandle latestCandle)
        {
            if (_dataStore == null || _periodStore == null)
                return;

            foreach (var periodType in Enum.GetValues<PeriodType>())
            {
                // Contribute Current period
                var current = _periodStore.GetCurrent(periodType);
                if (current != null && current.IsInitialized)
                {
                    ContributePeriod(current, periodType, true, current.PeriodStart, current.PeriodEnd);
                }

                // Contribute Previous period
                var previous = _periodStore.GetPrevious(periodType);
                if (previous != null && previous.IsInitialized)
                {
                    ContributePeriod(previous, periodType, false, previous.PeriodStart, previous.PeriodEnd);
                }
            }
        }

        private void ContributePeriod(PeriodData data, PeriodType periodType, bool isCurrent, DateTime periodStart, DateTime periodEnd)
        {
            if (_dataStore == null || CurrentBar == 0)
                return;

            var lastCandle = GetCandle(CurrentBar - 1);
            var candleDuration = GetCandleDurationMinutes();

            var timeRange = new TimeRange
            {
                Start = data.PeriodStart,
                End = lastCandle.Time.AddMinutes(candleDuration),
                Open = data.Open,
                High = data.High,
                Low = data.Low,
                Close = data.Close,
                OpenTime = data.PeriodStart,
                HighTime = data.HighTime,
                LowTime = data.LowTime,
                CloseTime = lastCandle.Time,
                CandleDurationMinutes = candleDuration,
                SourceId = _contributorId
            };

            _dataStore.ContributePeriodData(periodType, isCurrent, periodStart, periodEnd, timeRange);
        }

        /// <summary>
        /// Checks if we have adequate data coverage for a given period/level type.
        /// For High/Low/Mid levels, we need complete coverage of the period.
        /// For Open levels, we just need coverage at the start of the period.
        /// </summary>
        /// <param name="periodType">The period type to check.</param>
        /// <param name="isCurrent">True for current period, false for previous period.</param>
        /// <param name="requireComplete">True if we need full period coverage (for H/L/M), false for just the start (for Open).</param>
        /// <returns>True if we have adequate coverage.</returns>
        private bool HasAdequateCoverage(PeriodType periodType, bool isCurrent, bool requireComplete = true)
        {
            if (_dataStore == null)
                return false;

            var poi = _dataStore.GetPeriodPoi(periodType, isCurrent);
            if (poi == null)
                return false;

            if (requireComplete)
            {
                // For High/Low/Mid we need complete coverage
                return poi.HasCompleteCoverage();
            }
            else
            {
                // For Open we just need coverage at the start of the period
                return poi.HasCoverageAt(poi.PeriodStart);
            }
        }

        /// <summary>
        /// Logs the data store contents every 15 seconds for diagnostics.
        /// </summary>
        private void LogDataStoreContentsIfDue()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastDiagnosticLogTime).TotalSeconds < DiagnosticLogIntervalSeconds)
                return;
            
            _lastDiagnosticLogTime = now;
            
            if (_dataStore == null)
            {
                this.LogInfo("[DIAG] DataStore is NULL");
                return;
            }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[DIAG] DataStore for {_dataStore.Symbol} at {now:HH:mm:ss}:");
            
            foreach (var periodType in Enum.GetValues<PeriodType>())
            {
                foreach (var isCurrent in new[] { true, false })
                {
                    var poi = _dataStore.GetPeriodPoi(periodType, isCurrent);
                    if (poi != null)
                    {
                        var label = isCurrent ? "Current" : "Previous";
                        var periodEnd = poi.PeriodEnd == DateTime.MaxValue ? "ongoing" : poi.PeriodEnd.ToString("yyyy-MM-dd HH:mm");
                        sb.AppendLine($"  {periodType} ({label}): O={poi.Open} H={poi.High} L={poi.Low} C={poi.Close}");
                        sb.AppendLine($"    Period: {poi.PeriodStart:yyyy-MM-dd HH:mm} to {periodEnd}");
                        sb.AppendLine($"    Granularity: O={poi.OpenTimeGranularity}m H={poi.HighTimeGranularity}m L={poi.LowTimeGranularity}m C={poi.CloseTimeGranularity}m");
                        sb.AppendLine($"    Times: O={poi.OpenTime:yyyy-MM-dd HH:mm} H={poi.HighTime:yyyy-MM-dd HH:mm} L={poi.LowTime:yyyy-MM-dd HH:mm} C={poi.CloseTime:yyyy-MM-dd HH:mm}");
                        sb.AppendLine($"    Complete: {poi.HasCompleteCoverage()}");
                        
                        // Log covered ranges
                        var ranges = poi.CoveredRanges;
                        sb.AppendLine($"    CoveredRanges ({ranges.Count}):");
                        foreach (var range in ranges.Take(3)) // Limit to first 3
                        {
                            sb.AppendLine($"      {range.Start:yyyy-MM-dd HH:mm} to {range.End:yyyy-MM-dd HH:mm}");
                        }
                        if (ranges.Count > 3)
                            sb.AppendLine($"      ... and {ranges.Count - 3} more");
                        
                        // Log gaps if incomplete
                        if (!poi.HasCompleteCoverage())
                        {
                            var gaps = poi.GetGaps().Take(3).ToList();
                            sb.AppendLine($"    Gaps ({gaps.Count}):");
                            foreach (var gap in gaps)
                            {
                                sb.AppendLine($"      {gap.Start:yyyy-MM-dd HH:mm} to {gap.End:yyyy-MM-dd HH:mm}");
                            }
                        }
                    }
                }
            }
            
            // Add session and 4H diagnostic info
            sb.AppendLine();
            var current4h = _periodStore?.GetCurrent(PeriodType.FourHour);
            var previous4h = _periodStore?.GetPrevious(PeriodType.FourHour);
            sb.AppendLine($"  Current4H: {(current4h != null && current4h.IsInitialized ? current4h.PeriodStart.ToString("yyyy-MM-dd HH:mm") : "N/A")}");
            sb.AppendLine($"  Previous4H: {(previous4h != null && previous4h.IsInitialized ? previous4h.PeriodStart.ToString("yyyy-MM-dd HH:mm") : "N/A")}");
            if (CurrentBar > 0)
            {
                var lastCandle = GetCandle(CurrentBar - 1);
                sb.AppendLine($"  Last candle time: {lastCandle.Time:yyyy-MM-dd HH:mm}");
            }
            
            this.LogInfo(sb.ToString());
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

        /// <summary>
        /// Checks if the source chart timeframe provides day-level precision or better.
        /// Weekly and higher timeframes don't provide meaningful day timestamps for levels.
        /// </summary>
        private bool HasDailyPrecision()
        {
            // Daily candle = 1440 minutes (24 * 60)
            // Anything <= 1440 minutes has at least daily precision
            return GetCandleDurationMinutes() <= 1440;
        }

        #endregion

        #region Overrides

        protected override void OnRecalculate()
        {
            
            // NEW: Reset or create period store
            var timezone = InstrumentInfo?.TimeZone ?? 0;
            _periodStore = new TimeBasedPeriodStore(timezone);
            this.LogInfo($"OnRecalculate: store created with timezone {timezone}");
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            // Ensure data store is initialized for this instrument
            EnsureDataStoreInitialized();

            var candle = GetCandle(bar);
            
            // Feed session starts and candle data to the period store
            if (_periodStore != null)
            {
                if (IsNewSession(bar))
                {
                    _periodStore.SetSessionStart(candle.Time);
                }
                
                _periodStore.AddCandle(candle.Time, bar, candle.Open, candle.High, candle.Low, candle.Close);
            }


            // Contribute processed data to the aggregation layer
            ContributeDataToAggregator(candle);
            
            // Log diagnostics every 1000 bars
            if (bar > 0 && bar % 1000 == 0)
            {
                _periodStore?.LogDiagnostics(this);
            }
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            var renderSw = Stopwatch.StartNew();
            
            if (ChartInfo is null || Container is null)
                return;

            var region = Container.Region;
            var font = new RenderFont("Arial", FontSize);
            
            // Log data store contents every 15 seconds for diagnostics
            LogDataStoreContentsIfDue();
            
            // Collect all levels to draw
            var levels = GetDynamicLevels();
            
            // If rendering as rays, use TrendLines collection
            if (_renderAsRays)
            {
                RenderAsRaysMode(levels);
                
                // Still draw unavailable levels warning
                var unavailableLevels = GetUnavailableLevels();
                if (unavailableLevels.Count > 0)
                {
                    DrawUnavailableWarning(context, font, unavailableLevels, region);
                }
                // Fall through to also draw labels
            }

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

            // Calculate label positions with overlap prevention
            var labelPositions = CalculateLabelPositions(context, font, levels, region);

            // Draw each level with its calculated label position
            foreach (var pos in labelPositions)
            {
                DrawLevelWithBranch(context, font, pos, anchorX, region);
            }

            // Check for unavailable levels and draw warning
            var unavailable = GetUnavailableLevels();
            if (unavailable.Count > 0)
            {
                DrawUnavailableWarning(context, font, unavailable, region);
            }
            
            this.LogDebug($"OnRender: {levels.Count} levels in {renderSw.ElapsedMilliseconds}ms");
        }

        private void RenderAsRaysMode(List<KeyLevel> levels)
        {
            // Clear existing trend lines
            TrendLines.Clear();
            
            // Get visible bar range from chart
            int firstVisibleBar = FirstVisibleBarNumber;
            int lastVisibleBar = LastVisibleBarNumber;
            
            // Create rays for each level
            foreach (var level in levels)
            {
                // Resolve the level's timestamp to a bar index on THIS chart
                int rayStartBar = ResolveTimestampToBar(level.Date, firstVisibleBar, lastVisibleBar);
                
                // Convert CrossColor to System.Drawing.Color for pen
                var drawingColor = System.Drawing.Color.FromArgb(level.Color.A, level.Color.R, level.Color.G, level.Color.B);
                var pen = new CrossPen(drawingColor, 1);
                
                // Create a horizontal ray from the starting bar extending to the right
                var ray = new TrendLine(rayStartBar, level.Price, rayStartBar + 1, level.Price, pen)
                {
                    IsRay = true
                };
                
                TrendLines.Add(ray);
            }
        }

        /// <summary>
        /// Resolves a timestamp to a bar index on the current chart.
        /// If the timestamp is null or before the chart's first bar, returns firstVisibleBar.
        /// Uses binary search for efficiency.
        /// </summary>
        private int ResolveTimestampToBar(DateTime? timestamp, int firstVisibleBar, int lastVisibleBar)
        {
            if (!timestamp.HasValue || CurrentBar <= 0)
                return firstVisibleBar;

            var target = timestamp.Value;
            
            // If target is before the first candle on the chart, use first visible bar
            if (target <= GetCandle(0).Time)
                return firstVisibleBar;
            
            // Binary search through bars to find the one closest to the target timestamp
            int lo = 0, hi = CurrentBar - 1;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (GetCandle(mid).Time < target)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            
            // lo now points to the first bar with Time >= target, or CurrentBar-1
            // Check if the previous bar is actually closer
            if (lo > 0 && (target - GetCandle(lo - 1).Time).TotalMinutes < (GetCandle(lo).Time - target).TotalMinutes)
                lo--;
            
            return lo;
        }

        #endregion


        #region Level Collection

        private List<KeyLevel> GetDynamicLevels()
        {
            var levels = new List<KeyLevel>();
            
            // Query POI from singleton data store
            if (_dataStore == null)
                return levels;

            // Helper to get POI
            PeriodPoi? GetPoi(PeriodType type, bool isCurrent) => _dataStore.GetPeriodPoi(type, isCurrent);

            // Previous 4H High/Low - require complete coverage
            var prev4h = GetPoi(PeriodType.FourHour, false);
            if (_show4hHighLow && prev4h != null && prev4h.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prev4h.High, _useShortLabels ? "P4HH" : "Prev 4H High", _4hColor, prev4h.HighTime));
                levels.Add(new KeyLevel(prev4h.Low, _useShortLabels ? "P4HL" : "Prev 4H Low", _4hColor, prev4h.LowTime));
            }

            // Current 4H Open - just need period start
            var curr4h = GetPoi(PeriodType.FourHour, true);
            if (_show4hOpen && curr4h != null && curr4h.HasCoverageAt(curr4h.PeriodStart))
            {
                levels.Add(new KeyLevel(curr4h.Open, _useShortLabels ? "4HO" : "4H Open", _4hColor, curr4h.OpenTime));
            }

            // Previous 4H Mid - require complete coverage
            if (_show4hMid && prev4h != null && prev4h.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prev4h.Mid, _useShortLabels ? "P4HM" : "Prev 4H Mid", _4hColor, prev4h.OpenTime));
            }

            // Daily Open - just need period start
            var currDay = GetPoi(PeriodType.Daily, true);
            if (_showDailyOpen && currDay != null && currDay.HasCoverageAt(currDay.PeriodStart))
            {
                levels.Add(new KeyLevel(currDay.Open, _useShortLabels ? "DO" : "Day Open", _dailyColor, currDay.OpenTime));
            }

            // Previous Day High/Low - require complete coverage
            var prevDay = GetPoi(PeriodType.Daily, false);
            if (_showPrevDayHighLow && prevDay != null && prevDay.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevDay.High, _useShortLabels ? "PDH" : "Prev Day High", _dailyColor, prevDay.HighTime));
                levels.Add(new KeyLevel(prevDay.Low, _useShortLabels ? "PDL" : "Prev Day Low", _dailyColor, prevDay.LowTime));
            }

            // Previous Day Mid - require complete coverage
            if (_showPrevDayMid && prevDay != null && prevDay.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevDay.Mid, _useShortLabels ? "PDM" : "Prev Day Mid", _dailyColor, prevDay.OpenTime));
            }

            // Monday High/Low - prefer current week's Monday, fallback to previous
            var currMonday = GetPoi(PeriodType.Monday, true);
            var prevMonday = GetPoi(PeriodType.Monday, false);
            var usedMonday = (currMonday != null && currMonday.HasCompleteCoverage()) ? currMonday : prevMonday;
            bool isPrevMonday = usedMonday == prevMonday;
            
            if (_showMondayHighLow && usedMonday != null && usedMonday.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(usedMonday.High, _useShortLabels ? (isPrevMonday ? "PMDAYH" : "MDAYH") : (isPrevMonday ? "Prev Mon High" : "Mon High"), _mondayColor, usedMonday.HighTime));
                levels.Add(new KeyLevel(usedMonday.Low, _useShortLabels ? (isPrevMonday ? "PMDAYL" : "MDAYL") : (isPrevMonday ? "Prev Mon Low" : "Mon Low"), _mondayColor, usedMonday.LowTime));
            }

            // Monday Mid
            if (_showMondayMid && usedMonday != null && usedMonday.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(usedMonday.Mid, _useShortLabels ? (isPrevMonday ? "PMDAYM" : "MDAYM") : (isPrevMonday ? "Prev Mon Mid" : "Mon Mid"), _mondayColor, usedMonday.OpenTime));
            }

            // Quarterly Open - just need period start
            var currQuarter = GetPoi(PeriodType.Quarterly, true);
            if (_showQuarterlyOpen && currQuarter != null && currQuarter.HasCoverageAt(currQuarter.PeriodStart))
            {
                levels.Add(new KeyLevel(currQuarter.Open, _useShortLabels ? "QO" : "Quarter Open", _quarterlyColor, currQuarter.OpenTime));
            }

            // Previous Quarter High/Low - require complete coverage
            var prevQuarter = GetPoi(PeriodType.Quarterly, false);
            if (_showPrevQuarterHighLow && prevQuarter != null && prevQuarter.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevQuarter.High, _useShortLabels ? "PQH" : "Prev Quarter High", _quarterlyColor, prevQuarter.HighTime));
                levels.Add(new KeyLevel(prevQuarter.Low, _useShortLabels ? "PQL" : "Prev Quarter Low", _quarterlyColor, prevQuarter.LowTime));
            }

            // Previous Quarter Mid - require complete coverage
            if (_showPrevQuarterMid && prevQuarter != null && prevQuarter.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevQuarter.Mid, _useShortLabels ? "PQM" : "Prev Quarter Mid", _quarterlyColor, prevQuarter.OpenTime));
            }

            // Previous Year High/Low - require complete coverage
            var prevYear = GetPoi(PeriodType.Yearly, false);
            if (_showPrevYearHighLow && prevYear != null && prevYear.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevYear.High, _useShortLabels ? "PYH" : "Prev Year High", _yearlyColor, prevYear.HighTime));
                levels.Add(new KeyLevel(prevYear.Low, _useShortLabels ? "PYL" : "Prev Year Low", _yearlyColor, prevYear.LowTime));
            }

            // Previous Year Mid - require complete coverage
            if (_showPrevYearMid && prevYear != null && prevYear.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevYear.Mid, _useShortLabels ? "PYM" : "Prev Year Mid", _yearlyColor, prevYear.OpenTime));
            }

            // Current Year High/Low - require complete coverage
            var currYear = GetPoi(PeriodType.Yearly, true);
            if (_showCurrentYearHighLow && currYear != null && currYear.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(currYear.High, _useShortLabels ? "CYH" : "Year High", _yearlyColor, currYear.HighTime));
                levels.Add(new KeyLevel(currYear.Low, _useShortLabels ? "CYL" : "Year Low", _yearlyColor, currYear.LowTime));
            }

            // Current Year Mid - require complete coverage
            if (_showCurrentYearMid && currYear != null && currYear.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(currYear.Mid, _useShortLabels ? "CYM" : "Year Mid", _yearlyColor, currYear.OpenTime));
            }

            // Week Open - just need period start
            var currWeek = GetPoi(PeriodType.Weekly, true);
            if (_showWeekOpen && currWeek != null && currWeek.HasCoverageAt(currWeek.PeriodStart))
            {
                levels.Add(new KeyLevel(currWeek.Open, _useShortLabels ? "WO" : "Week Open", _weeklyColor, currWeek.OpenTime));
            }

            // Previous Week High/Low - require complete coverage
            var prevWeek = GetPoi(PeriodType.Weekly, false);
            if (_showPrevWeekHighLow && prevWeek != null && prevWeek.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevWeek.High, _useShortLabels ? "PWH" : "Prev Week High", _weeklyColor, prevWeek.HighTime));
                levels.Add(new KeyLevel(prevWeek.Low, _useShortLabels ? "PWL" : "Prev Week Low", _weeklyColor, prevWeek.LowTime));
            }

            // Previous Week Mid - require complete coverage
            if (_showPrevWeekMid && prevWeek != null && prevWeek.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevWeek.Mid, _useShortLabels ? "PWM" : "Prev Week Mid", _weeklyColor, prevWeek.OpenTime));
            }

            // Month Open - just need period start
            var currMonth = GetPoi(PeriodType.Monthly, true);
            if (_showMonthOpen && currMonth != null && currMonth.HasCoverageAt(currMonth.PeriodStart))
            {
                levels.Add(new KeyLevel(currMonth.Open, _useShortLabels ? "MO" : "Month Open", _monthlyColor, currMonth.OpenTime));
            }

            // Previous Month High/Low - require complete coverage
            var prevMonth = GetPoi(PeriodType.Monthly, false);
            if (_showPrevMonthHighLow && prevMonth != null && prevMonth.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevMonth.High, _useShortLabels ? "PMH" : "Prev Month High", _monthlyColor, prevMonth.HighTime));
                levels.Add(new KeyLevel(prevMonth.Low, _useShortLabels ? "PML" : "Prev Month Low", _monthlyColor, prevMonth.LowTime));
            }

            // Previous Month Mid - require complete coverage
            if (_showPrevMonthMid && prevMonth != null && prevMonth.HasCompleteCoverage())
            {
                levels.Add(new KeyLevel(prevMonth.Mid, _useShortLabels ? "PMM" : "Prev Month Mid", _monthlyColor, prevMonth.OpenTime));
            }

            return levels;
        }

        private List<string> GetUnavailableLevels()
        {
            var unavailable = new List<string>();

            // 4H checks - require complete coverage for H/L/Mid, just start for Open
            if (_show4hOpen && !HasAdequateCoverage(PeriodType.FourHour, true, requireComplete: false))
                unavailable.Add("4H Open");
            if (_show4hHighLow && !HasAdequateCoverage(PeriodType.FourHour, false, requireComplete: true))
                unavailable.Add("4H H/L");
            if (_show4hMid && !HasAdequateCoverage(PeriodType.FourHour, false, requireComplete: true))
                unavailable.Add("4H Mid");

            // Daily checks
            if (_showDailyOpen && !HasAdequateCoverage(PeriodType.Daily, true, requireComplete: false))
                unavailable.Add("Day Open");
            if (_showPrevDayHighLow && !HasAdequateCoverage(PeriodType.Daily, false, requireComplete: true))
                unavailable.Add("PD H/L");
            if (_showPrevDayMid && !HasAdequateCoverage(PeriodType.Daily, false, requireComplete: true))
                unavailable.Add("PD Mid");

            // Monday checks - check current, then previous
            bool hasCurrentMonday = HasAdequateCoverage(PeriodType.Monday, true, requireComplete: true);
            bool hasPreviousMonday = HasAdequateCoverage(PeriodType.Monday, false, requireComplete: true);
            if (_showMondayHighLow && !hasCurrentMonday && !hasPreviousMonday)
                unavailable.Add("Mon H/L");
            if (_showMondayMid && !hasCurrentMonday && !hasPreviousMonday)
                unavailable.Add("Mon Mid");

            // Quarterly checks
            if (_showQuarterlyOpen && !HasAdequateCoverage(PeriodType.Quarterly, true, requireComplete: false))
                unavailable.Add("Q Open");
            if (_showPrevQuarterHighLow && !HasAdequateCoverage(PeriodType.Quarterly, false, requireComplete: true))
                unavailable.Add("PQ H/L");
            if (_showPrevQuarterMid && !HasAdequateCoverage(PeriodType.Quarterly, false, requireComplete: true))
                unavailable.Add("PQ Mid");

            // Yearly checks
            if (_showPrevYearHighLow && !HasAdequateCoverage(PeriodType.Yearly, false, requireComplete: true))
                unavailable.Add("PY H/L");
            if (_showPrevYearMid && !HasAdequateCoverage(PeriodType.Yearly, false, requireComplete: true))
                unavailable.Add("PY Mid");
            if (_showCurrentYearHighLow && !HasAdequateCoverage(PeriodType.Yearly, true, requireComplete: true))
                unavailable.Add("CY H/L");
            if (_showCurrentYearMid && !HasAdequateCoverage(PeriodType.Yearly, true, requireComplete: true))
                unavailable.Add("CY Mid");

            // Weekly checks (full week)
            if (_showWeekOpen && !HasAdequateCoverage(PeriodType.Weekly, true, requireComplete: false))
                unavailable.Add("W Open");
            if (_showPrevWeekHighLow && !HasAdequateCoverage(PeriodType.Weekly, false, requireComplete: true))
                unavailable.Add("PW H/L");
            if (_showPrevWeekMid && !HasAdequateCoverage(PeriodType.Weekly, false, requireComplete: true))
                unavailable.Add("PW Mid");

            // Monthly checks
            if (_showMonthOpen && !HasAdequateCoverage(PeriodType.Monthly, true, requireComplete: false))
                unavailable.Add("M Open");
            if (_showPrevMonthHighLow && !HasAdequateCoverage(PeriodType.Monthly, false, requireComplete: true))
                unavailable.Add("PM H/L");
            if (_showPrevMonthMid && !HasAdequateCoverage(PeriodType.Monthly, false, requireComplete: true))
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

            // Build display text with optional date
            // Only show dates if the chart timeframe provides daily precision or better
            string displayText = level.Label;
            if (_levelDateFormat != LevelDateFormat.Off && level.Date.HasValue && HasDailyPrecision())
            {
                string dateStr = _levelDateFormat == LevelDateFormat.MonthDay
                    ? level.Date.Value.ToString("MM/dd")
                    : level.Date.Value.ToString("dd/MM");
                displayText = $"{level.Label} {dateStr}";
            }

            // Draw the text label
            var textSize = context.MeasureString(displayText, font);

            Rectangle textRect;
            if (Anchor == AnchorPosition.Right)
            {
                textRect = new Rectangle(textX - textSize.Width, labelY - textSize.Height / 2, textSize.Width, textSize.Height);
            }
            else
            {
                textRect = new Rectangle(textX, labelY - textSize.Height / 2, textSize.Width, textSize.Height);
            }

            context.DrawString(displayText, font, TextColor.Convert(), textRect, _labelFormat);
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
