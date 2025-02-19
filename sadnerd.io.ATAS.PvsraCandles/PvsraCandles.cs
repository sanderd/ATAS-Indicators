using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using OFT.Attributes;
using OFT.Localization;

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
        private CrossColor _pvsraNeutralPositiveColor = CrossColors.LightGray;
        private CrossColor _pvsraNeutralNegativeColor = CrossColors.DarkGray;

        //private PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
        //{
        //    IsHidden = true
        //}; 
        private readonly PaintbarsDataSeries _renderSeries = new("ColorBars", Strings.Candles) { IsHidden = true };

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
        }

        protected override void OnRecalculate()
        {
            Clear();
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar < 10)
                return;

            var currentCandle = GetCandle(bar);
            var prevCandles = Enumerable.Range(1, 10).Select(i => GetCandle(bar - i)).ToArray();

            var averagePreviousVolume = prevCandles.Average(c => c.Volume);
            var highestPreviousVolumeSpread = prevCandles.Max(c => c.Volume * (c.High - c.Low));
            var currentVolumeSpread = currentCandle.Volume * (currentCandle.High - currentCandle.Low);

            if(currentCandle.Volume >= 2 * averagePreviousVolume || currentVolumeSpread >= highestPreviousVolumeSpread)
            {
                _renderSeries[bar] = currentCandle.Close > currentCandle.Open ? PvsraGreenColor : PvsraRedColor;
            }
            else if (currentCandle.Volume >= (decimal)1.5 * averagePreviousVolume)
            {
                _renderSeries[bar] = currentCandle.Close > currentCandle.Open ? PvsraBlueColor : PvsraVioletColor;
            }
            else
            {
                _renderSeries[bar] = currentCandle.Close > currentCandle.Open ? PvsraNeutralPositiveColor : PvsraNeutralNegativeColor;
            }
        }
    }
}
