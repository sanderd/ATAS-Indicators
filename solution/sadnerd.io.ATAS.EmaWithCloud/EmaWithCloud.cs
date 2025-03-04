using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using OFT.Localization;
using Utils.Common.Attributes;

namespace sadnerd.io.ATAS.EmaWithCloud
{
    [DisplayName("EMA With Cloud")]
    [Display(Name = "EMA With Cloud", Description = "EMA With Cloud")]
    [HelpLink("https://github.com/sanderd/ATAS-Indicators/wiki/EMA-With-Cloud")]
    public class EmaWithCloud : Indicator
    {
        private EMA _ema = new();
        private StdDev _stdDev = new();

        private static readonly CrossColor DefaultEmaLineColor = CrossColor.FromArgb(255, 129, 104, 186);
        private static readonly CrossColor DefaultEmaCloudColor = CrossColor.FromArgb(40, 82, 45, 168);
        private static readonly CrossColor DefaultEmaCloudBorderColor = CrossColor.FromArgb(255, 81, 45, 168);

        private int _emaPeriod = 50;
        private decimal _emaCloudPeriodFactor = 2;
        private decimal _emaCloudWidthFactor = 4;

        private readonly ValueDataSeries _renderSeries = new("RenderSeries", "EMA Line")
        {
            Color = DefaultEmaLineColor,
            VisualType = VisualMode.Line,
            IsHidden = false,
            DrawAbovePrice = true,
            ShowCurrentValue = true,
            ShowZeroValue = false,
            ShowTooltip = false
        };

        private readonly ValueDataSeries _cloudLowerBorder = new("Lower EMA Cloud Border", "Lower EMA Cloud Border")
        {
            Color = DefaultEmaCloudBorderColor,
            VisualType = VisualMode.Line,
            IsHidden = false,
            DrawAbovePrice = false,
            ShowCurrentValue = false,
            ShowZeroValue = false,
            ShowTooltip = false
        };

        private readonly ValueDataSeries _cloudUpperBorder = new("Upper EMA Cloud Border", "Upper EMA Cloud Border")
        {
            Color = DefaultEmaCloudBorderColor,
            VisualType = VisualMode.Line,
            IsHidden = false,
            DrawAbovePrice = false,
            ShowCurrentValue = false,
            ShowZeroValue = false,
            ShowTooltip = false
        };

        private readonly RangeDataSeries _cloudLower = new("Lower EMA Cloud", "Lower EMA Cloud")
        {
            RangeColor = DefaultEmaCloudColor,
            DescriptionKey = nameof(Strings.ChannelNegativeAreaSettingsDescription),
            IsHidden = false,
            DrawAbovePrice = false
        };

        private readonly RangeDataSeries _cloudUpper = new("Upper EMA Cloud", "Upper EMA Cloud")
        {
            RangeColor = DefaultEmaCloudColor,
            DescriptionKey = nameof(Strings.ChannelNegativeAreaSettingsDescription),
            IsHidden = false,
            DrawAbovePrice = false
        };

        [OFT.Attributes.Parameter]
        [Display(Name = "EMA Period", GroupName = "Settings")]
        [Range(1, 10000)]
        public int EmaPeriod
        {
            get => _emaPeriod;
            set
            {
                _emaPeriod = value;
                RecalculateValues();
            }
        }

        [OFT.Attributes.Parameter]
        [Display(Name = "EMA Cloud Period Factor", GroupName = "Settings")]
        [Range(1, 10000)]
        public decimal EmaCloudPeriodFactor
        {
            get => _emaCloudPeriodFactor;
            set
            {
                _emaCloudPeriodFactor = value;
                RecalculateValues();
            }
        }

        [OFT.Attributes.Parameter]
        [Display(Name = "EMA Cloud Width Factor", GroupName = "Settings")]
        [Range(1, 10000)]
        public decimal EmaCloudWidthFactor
        {
            get => _emaCloudWidthFactor;
            set
            {
                _emaCloudWidthFactor = value;
                RecalculateValues();
            }
        }

        public EmaWithCloud()
        {
            DataSeries[0] = _renderSeries;
            DataSeries.Add(_cloudLowerBorder);
            DataSeries.Add(_cloudUpperBorder);
            DataSeries.Add(_cloudLower);
            DataSeries.Add(_cloudUpper);
        }

        protected override void OnRecalculate()
        {
            _ema = new EMA();
            _ema.Period = EmaPeriod;
            _stdDev = new StdDev();
            _stdDev.Period = (int)decimal.Floor(EmaPeriod * EmaCloudPeriodFactor);
            base.OnRecalculate();
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            _ema.Calculate(bar, value);
            _stdDev.Calculate(bar, value);

            if (bar == 0)
            {
                _cloudLowerBorder.Clear();
                _cloudUpperBorder.Clear();
                _renderSeries.Clear();
            }

            _renderSeries[bar] = _ema[bar];
            _cloudLowerBorder[bar] = _ema[bar] - _stdDev[bar] / EmaCloudWidthFactor;
            _cloudUpperBorder[bar] = _ema[bar] + _stdDev[bar] / EmaCloudWidthFactor;

            _cloudLower[bar].Upper = _renderSeries[bar];
            _cloudLower[bar].Lower = _cloudLowerBorder[bar];

            _cloudUpper[bar].Upper = _cloudUpperBorder[bar];
            _cloudUpper[bar].Lower = _renderSeries[bar];
        }
    }
}
