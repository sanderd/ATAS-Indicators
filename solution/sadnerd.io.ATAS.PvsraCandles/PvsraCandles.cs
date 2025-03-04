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
        private IDictionary<int, Shadow> _shadows = new Dictionary<int, Shadow>();
        private PenSettings _shadowBorderPen = new() { Color = CrossColors.Transparent };

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
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null) return;

            if (ShowShadows)
            {
                DrawCandleShadows(context, ChartInfo, ChartInfo.TimeFrame);
            }
        }

        private void DrawCandleShadows(RenderContext context, IChart chartInfo, string timeFrame)
        {
            var shadows = _shadows.Where(s => s.Key <= LastVisibleBarNumber && s.Value.EndBar == null).ToList();

            foreach (var shadow in shadows)
            {
                var x = chartInfo.GetXByBar(shadow.Key);
                var x2 = chartInfo.Region.Width;
                var y = chartInfo.GetYByPrice(Math.Max(shadow.Value.UnrecoveredPriceHigh, shadow.Value.UnrecoveredPriceLow), false);
                var w = x2 - x;
                var h = chartInfo.GetYByPrice(Math.Min(shadow.Value.UnrecoveredPriceHigh, shadow.Value.UnrecoveredPriceLow), false) - y;
                var rec = new Rectangle(x, y, w, h);
                context.DrawFillRectangle(_shadowBorderPen.RenderObject, ShadowColor.Convert(), rec);
            }
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
                    CreateCandleShadow(bar, currentCandle, candleType);
                }

                MarkRecoveredShadows(bar, currentCandle);
            }
        }

        private void CreateCandleShadow(int bar, CandleDetails currentCandle, CandleType candleType)
        {
            if (_shadows.ContainsKey(bar))
            {
                _shadows.Remove(bar);
            }

            var priceHigh = Math.Max(currentCandle.Open, currentCandle.Close);
            var priceLow = Math.Min(currentCandle.Open, currentCandle.Close);
            
            _shadows.Add(bar, new Shadow(bar, priceLow, priceHigh, null, priceLow, priceHigh));
        }

        private void MarkRecoveredShadows(int bar, CandleDetails currentCandle)
        {
            var shadows = _shadows.Where(s => s.Key < bar && s.Value.EndBar == null).ToList();

            var priceHigh = currentCandle.High;
            var priceLow = currentCandle.Low;

            foreach (var shadow in shadows)
            {
                if(priceLow > shadow.Value.UnrecoveredPriceHigh || priceHigh < shadow.Value.UnrecoveredPriceLow) continue;

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
                }
            }
        }
    }
}
