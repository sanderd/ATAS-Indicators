using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using sadnerd.io.ATAS.PvsraCandles.Engines;
using sadnerd.io.ATAS.PvsraCandles.Enums;
using sadnerd.io.ATAS.PvsraCandles.Mappers;
using sadnerd.io.ATAS.PvsraCandles.Models;

namespace sadnerd.io.ATAS.PvsraCandles
{
    [DisplayName("PVSRA Candles")]
    [Display(Name = "PVSRA Candles", Description = "Candle colors are based on average volume of previous candles")]
    [HelpLink("https://github.com/sanderd/ATAS-Indicators/wiki/PVSRA-Candles")]
    public class PvsraCandles : Indicator
    {
        private CrossColor _pvsraGreenColor = CrossColors.LightGreen;
        private CrossColor _pvsraRedColor = CrossColors.Red;
        private CrossColor _pvsraBlueColor = CrossColor.FromArgb(255, 0, 136, 255);
        private CrossColor _pvsraVioletColor = CrossColor.FromArgb(255, 217, 0, 217);
        private CrossColor _pvsraNeutralPositiveColor = CrossColor.FromArgb(255, 134, 134, 134);
        private CrossColor _pvsraNeutralNegativeColor = CrossColor.FromArgb(255, 89, 89, 89);
        private CrossColor _shadowColor = CrossColor.FromArgb(20, 255, 255, 255);

        private readonly PaintbarsDataSeries _renderSeries = new("ColorBars", Strings.Candles) { IsHidden = true };
        private bool _showShadows;
        
        private readonly IIndicatorCandleToCandleDetailsMapper _candleMapper;
        private readonly ICandleTypeDeterminator _candleTypeDeterminator;
        private PenSettings _shadowBorderPen = new() { Color = CrossColors.Transparent };

        /// <summary>
        /// Working state, owned exclusively by the calculation thread. Never touched while rendering.
        /// </summary>
        private readonly Dictionary<int, Shadow> _shadows = new();

        /// <summary>
        /// Immutable snapshot handed to the render thread. Replaced wholesale, never mutated in place:
        /// the renderer only ever sees a fully built array, so there is nothing to enumerate mid-change
        /// and no torn <see cref="decimal"/> reads.
        /// </summary>
        private ShadowBox[] _renderShadows = Array.Empty<ShadowBox>();

        private bool _shadowsDirty;

        private readonly record struct ShadowBox(int Bar, decimal Low, decimal High);

        [Display(Name = "PVSRA Green Candle", GroupName = "Candles")]
        public CrossColor PvsraGreenColor
        {
            get => _pvsraGreenColor;
            set
            {
                _pvsraGreenColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Red Candle", GroupName = "Candles")]
        public CrossColor PvsraRedColor
        {
            get => _pvsraRedColor;
            set
            {
                _pvsraRedColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Blue Candle", GroupName = "Candles")]
        public CrossColor PvsraBlueColor
        {
            get => _pvsraBlueColor;
            set
            {
                _pvsraBlueColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Violet Candle", GroupName = "Candles")]
        public CrossColor PvsraVioletColor
        {
            get => _pvsraVioletColor;
            set
            {
                _pvsraVioletColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Positive Candle", GroupName = "Candles")]
        public CrossColor PvsraNeutralPositiveColor
        {
            get => _pvsraNeutralPositiveColor;
            set
            {
                _pvsraNeutralPositiveColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Negative Candle", GroupName = "Candles")]
        public CrossColor PvsraNeutralNegativeColor
        {
            get => _pvsraNeutralNegativeColor;
            set
            {
                _pvsraNeutralNegativeColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show shadows", GroupName = "Shadows")]
        public bool ShowShadows
        {
            get => this._showShadows;
            set
            {
                _showShadows = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Shadow fill color", GroupName = "Shadows")]
        public CrossColor ShadowColor
        {
            get => _shadowColor;
            set
            {
                _shadowColor = value;
                RecalculateValues();
            }
        }

        public PvsraCandles() : base(true)
        {
            DenyToChangePanel = true;
            DataSeries[0] = _renderSeries;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);

            _candleMapper = new IndicatorCandleToCandleDetailsMapper();
            _candleTypeDeterminator = new CandleTypeDeterminator();
        }

        protected override void OnRecalculate()
        {
            Clear();
            _shadows.Clear();
            _shadowsDirty = false;
            Volatile.Write(ref _renderShadows, Array.Empty<ShadowBox>());
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null) return;

            if (ShowShadows)
            {
                DrawCandleShadows(context, ChartInfo);
            }
        }

        private void DrawCandleShadows(RenderContext context, IChart chartInfo)
        {
            var shadows = Volatile.Read(ref _renderShadows);
            if (shadows.Length == 0) return;

            var lastVisibleBar = LastVisibleBarNumber;
            var regionWidth = chartInfo.Region.Width;

            foreach (var shadow in shadows)
            {
                if (shadow.Bar > lastVisibleBar) continue;

                var x = chartInfo.GetXByBar(shadow.Bar);
                var y = chartInfo.GetYByPrice(Math.Max(shadow.High, shadow.Low), false);
                var w = regionWidth - x;
                var h = chartInfo.GetYByPrice(Math.Min(shadow.High, shadow.Low), false) - y;
                var rec = new Rectangle(x, y, w, h);
                context.DrawFillRectangle(_shadowBorderPen.RenderObject, ShadowColor.Convert(), rec);
            }
        }

        /// <summary>
        /// Rebuilds the render snapshot from the working state and publishes it in one atomic reference write.
        /// </summary>
        private void PublishShadows()
        {
            var boxes = new List<ShadowBox>(_shadows.Count);

            foreach (var shadow in _shadows.Values)
            {
                if (shadow.EndBar != null) continue;

                boxes.Add(new ShadowBox(shadow.StartBar, shadow.UnrecoveredPriceLow, shadow.UnrecoveredPriceHigh));
            }

            Volatile.Write(ref _renderShadows, boxes.ToArray());
            _shadowsDirty = false;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            // We need at least 10 previous bars to calculate the PVSRA candles
            if (bar < 10)
                return;

            var currentCandle = _candleMapper.Map(GetCandle(bar));
            var prevCandles = Enumerable.Range(1, 10).Select(i => _candleMapper.Map(GetCandle(bar - i))).ToArray();

            var candleType = _candleTypeDeterminator.GetCandleType(currentCandle, prevCandles);

            switch (candleType)
            {
                case CandleType.Green:
                    _renderSeries[bar] = PvsraGreenColor;
                    break;
                case CandleType.Red:
                    _renderSeries[bar] = PvsraRedColor;
                    break;
                case CandleType.Blue:
                    _renderSeries[bar] = PvsraBlueColor;
                    break;
                case CandleType.Violet:
                    _renderSeries[bar] = PvsraVioletColor;
                    break;
                case CandleType.NeutralPositive:
                    _renderSeries[bar] = PvsraNeutralPositiveColor;
                    break;
                case CandleType.NeutralNegative:
                    _renderSeries[bar] = PvsraNeutralNegativeColor;
                    break;
            }

            if (_showShadows)
            {
                if (candleType != CandleType.NeutralPositive && candleType != CandleType.NeutralNegative)
                {
                    CreateCandleShadow(bar, currentCandle);
                }

                MarkRecoveredShadows(bar, currentCandle);

                // Publishing on every historical bar would allocate a snapshot per bar for no visible gain:
                // the chart only shows the result once the pass reaches the live bars.
                if (_shadowsDirty && bar >= CurrentBar - 1)
                {
                    PublishShadows();
                }
            }
        }

        private void CreateCandleShadow(int bar, CandleDetails currentCandle)
        {
            var priceHigh = Math.Max(currentCandle.Open, currentCandle.Close);
            var priceLow = Math.Min(currentCandle.Open, currentCandle.Close);

            // The live bar is recalculated on every tick; only republish when the shadow actually moved.
            if (_shadows.TryGetValue(bar, out var existing)
                && existing.EndBar == null
                && existing.UnrecoveredPriceLow == priceLow
                && existing.UnrecoveredPriceHigh == priceHigh)
            {
                return;
            }

            _shadows[bar] = new Shadow(bar, priceLow, priceHigh, null, priceLow, priceHigh);
            _shadowsDirty = true;
        }

        private void MarkRecoveredShadows(int bar, CandleDetails currentCandle)
        {
            var shadows = _shadows.Where(s => s.Key < bar && s.Value.EndBar == null).ToList();

            var priceHigh = currentCandle.High;
            var priceLow = currentCandle.Low;

            foreach (var shadow in shadows)
            {
                if(priceLow > shadow.Value.UnrecoveredPriceHigh || priceHigh < shadow.Value.UnrecoveredPriceLow) continue;

                var previousLow = shadow.Value.UnrecoveredPriceLow;
                var previousHigh = shadow.Value.UnrecoveredPriceHigh;

                if (priceLow <= shadow.Value.UnrecoveredPriceLow && priceHigh >= shadow.Value.UnrecoveredPriceHigh)
                {
                    shadow.Value.UnrecoveredPriceHigh = shadow.Value.UnrecoveredPriceLow;
                } else if (priceLow >= shadow.Value.UnrecoveredPriceLow)
                {
                    shadow.Value.UnrecoveredPriceHigh = Math.Min(shadow.Value.UnrecoveredPriceHigh, priceLow);
                } else if (priceHigh <= shadow.Value.UnrecoveredPriceHigh)
                {
                    shadow.Value.UnrecoveredPriceLow = Math.Max(shadow.Value.UnrecoveredPriceLow, priceHigh);
                }

                if (shadow.Value.UnrecoveredPriceHigh <= shadow.Value.UnrecoveredPriceLow)
                {
                    shadow.Value.EndBar = Math.Min(shadow.Value.EndBar ?? int.MaxValue, bar);
                    _shadowsDirty = true;
                }
                else if (shadow.Value.UnrecoveredPriceLow != previousLow || shadow.Value.UnrecoveredPriceHigh != previousHigh)
                {
                    _shadowsDirty = true;
                }
            }
        }
    }
}
