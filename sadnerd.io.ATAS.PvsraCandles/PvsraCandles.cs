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
    [Display(Name = "PVSRA Candles", Description = "example description")]
    [HelpLink("https://example.org")]
    public class PvsraCandles : Indicator
    {
        private CrossColor _pvsraGreenColor = CrossColors.LightGreen;
        private CrossColor _pvsraRedColor = CrossColors.Red;
        private CrossColor _pvsraBlueColor = CrossColor.FromArgb(255, 0, 136, 255);
        private CrossColor _pvsraVioletColor = CrossColor.FromArgb(255, 217, 0, 217);
        private CrossColor _pvsraNeutralPositiveColor = CrossColor.FromArgb(255, 134, 134, 134);
        private CrossColor _pvsraNeutralNegativeColor = CrossColor.FromArgb(255, 89, 89, 89);
        private CrossColor _vectorShadowColor = CrossColor.FromArgb(20, 255, 255, 255);

        //private PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
        //{
        //    IsHidden = true
        //}; 
        private readonly PaintbarsDataSeries _renderSeries = new("ColorBars", Strings.Candles) { IsHidden = true };
        private bool _showVectorShadows;
        
        private readonly IIndicatorCandleToCandleDetailsMapper _candleMapper;
        private readonly ICandleTypeDeterminator _candleTypeDeterminator;
        private IDictionary<int, VectorShadow> _vectorShadows = new Dictionary<int, VectorShadow>();
        private PenSettings _vectorShadowPen = new() { Color = CrossColors.Transparent };

        [Display(Name = "PVSRA Green Vector", GroupName = "Vectors")]
        public CrossColor PvsraGreenColor
        {
            get => _pvsraGreenColor;
            set
            {
                _pvsraGreenColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Red Vector", GroupName = "Vectors")]
        public CrossColor PvsraRedColor
        {
            get => _pvsraRedColor;
            set
            {
                _pvsraRedColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Blue Vector", GroupName = "Vectors")]
        public CrossColor PvsraBlueColor
        {
            get => _pvsraBlueColor;
            set
            {
                _pvsraBlueColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Violet Vector", GroupName = "Vectors")]
        public CrossColor PvsraVioletColor
        {
            get => _pvsraVioletColor;
            set
            {
                _pvsraVioletColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Positive Vector", GroupName = "Vectors")]
        public CrossColor PvsraNeutralPositiveColor
        {
            get => _pvsraNeutralPositiveColor;
            set
            {
                _pvsraNeutralPositiveColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Negative Vector", GroupName = "Vectors")]
        public CrossColor PvsraNeutralNegativeColor
        {
            get => _pvsraNeutralNegativeColor;
            set
            {
                _pvsraNeutralNegativeColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Show vector shadows", GroupName = "Shadows")]
        public bool ShowVectorShadows
        {
            get => this._showVectorShadows;
            set
            {
                _showVectorShadows = value;
                RecalculateValues();
            }
        }

        [Display(Name = "Vector shadow fill color", GroupName = "Shadows")]
        public CrossColor VectorShadowColor
        {
            get => _vectorShadowColor;
            set
            {
                _vectorShadowColor = value;
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
            _vectorShadows.Clear();
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo is null) return;

            if (_showVectorShadows)
            {
                DrawVectorCandles(context, ChartInfo, ChartInfo.TimeFrame);
            }
        }

        // Based on FairValueGap
        private void DrawVectorCandles(RenderContext context, IChart chartInfo, string timeFrame)
        {
            var shadows = _vectorShadows.Where(s => s.Key <= LastVisibleBarNumber && s.Value.EndBar == null).ToList();

            foreach (var shadow in shadows)
            {
                var x = chartInfo.GetXByBar(shadow.Key);
                var x2 = chartInfo.Region.Width;
                var y = chartInfo.GetYByPrice(Math.Max(shadow.Value.UnrecoveredPriceHigh, shadow.Value.UnrecoveredPriceLow), false);
                var w = x2 - x;
                var h = chartInfo.GetYByPrice(Math.Min(shadow.Value.UnrecoveredPriceHigh, shadow.Value.UnrecoveredPriceLow), false) - y;
                var rec = new Rectangle(x, y, w, h);
                context.DrawFillRectangle(_vectorShadowPen.RenderObject, VectorShadowColor.Convert(), rec);
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
                case CandleType.GreenVector:
                    _renderSeries[bar] = PvsraGreenColor;
                    break;
                case CandleType.RedVector:
                    _renderSeries[bar] = PvsraRedColor;
                    break;
                case CandleType.BlueVector:
                    _renderSeries[bar] = PvsraBlueColor;
                    break;
                case CandleType.VioletVector:
                    _renderSeries[bar] = PvsraVioletColor;
                    break;
                case CandleType.NeutralPositive:
                    _renderSeries[bar] = PvsraNeutralPositiveColor;
                    break;
                case CandleType.NeutralNegative:
                    _renderSeries[bar] = PvsraNeutralNegativeColor;
                    break;
            }

            if (_showVectorShadows)
            {
                if (candleType != CandleType.NeutralPositive && candleType != CandleType.NeutralNegative)
                {
                    CreateVectorShadow(bar, currentCandle, candleType);
                }

                MarkVectorsRecovered(bar, currentCandle);
            }
        }

        private void CreateVectorShadow(int bar, CandleDetails currentCandle, CandleType candleType)
        {
            if (_vectorShadows.ContainsKey(bar))
            {
                _vectorShadows.Remove(bar);
            }

            var priceHigh = Math.Max(currentCandle.Open, currentCandle.Close);
            var priceLow = Math.Min(currentCandle.Open, currentCandle.Close);
            
            _vectorShadows.Add(bar, new VectorShadow(bar, priceLow, priceHigh, null, priceLow, priceHigh));
        }

        private void MarkVectorsRecovered(int bar, CandleDetails currentCandle)
        {
            var shadows = _vectorShadows.Where(s => s.Key < bar && s.Value.EndBar == null).ToList();

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
