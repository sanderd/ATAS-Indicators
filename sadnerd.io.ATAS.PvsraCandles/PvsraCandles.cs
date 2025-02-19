using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using OFT.Attributes;
using OFT.Localization;
using sadnerd.io.ATAS.PvsraCandles.Engines;
using sadnerd.io.ATAS.PvsraCandles.Enums;
using sadnerd.io.ATAS.PvsraCandles.Mappers;

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

        //private PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
        //{
        //    IsHidden = true
        //}; 
        private readonly PaintbarsDataSeries _renderSeries = new("ColorBars", Strings.Candles) { IsHidden = true };
        
        private readonly IIndicatorCandleToCandleDetailsMapper _candleMapper;
        private readonly ICandleTypeDeterminator _candleTypeDeterminator;

        [Display(Name = "PVSRA Green Vector", GroupName = "Colors")]
        public CrossColor PvsraGreenColor
        {
            get => _pvsraGreenColor;
            set
            {
                _pvsraGreenColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Red Vector", GroupName = "Colors")]
        public CrossColor PvsraRedColor
        {
            get => _pvsraRedColor;
            set
            {
                _pvsraRedColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Blue Vector", GroupName = "Colors")]
        public CrossColor PvsraBlueColor
        {
            get => _pvsraBlueColor;
            set
            {
                _pvsraBlueColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Violet Vector", GroupName = "Colors")]
        public CrossColor PvsraVioletColor
        {
            get => _pvsraVioletColor;
            set
            {
                _pvsraVioletColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Positive Vector", GroupName = "Colors")]
        public CrossColor PvsraNeutralPositiveColor
        {
            get => _pvsraNeutralPositiveColor;
            set
            {
                _pvsraNeutralPositiveColor = value;
                RecalculateValues();
            }
        }

        [Display(Name = "PVSRA Neutral Negative Vector", GroupName = "Colors")]
        public CrossColor PvsraNeutralNegativeColor
        {
            get => _pvsraNeutralNegativeColor;
            set
            {
                _pvsraNeutralNegativeColor = value;
                RecalculateValues();
            }
        }

        public PvsraCandles() : base(true)
        {
            DenyToChangePanel = true;
            DataSeries[0] = _renderSeries;

            _candleMapper = new IndicatorCandleToCandleDetailsMapper();
            _candleTypeDeterminator = new CandleTypeDeterminator();

        }

        protected override void OnRecalculate()
        {
            Clear();
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
        }
    }
}
