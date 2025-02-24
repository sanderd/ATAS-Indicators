using ATAS.Indicators;
using sadnerd.io.ATAS.PvsraCandles.Models;

namespace sadnerd.io.ATAS.PvsraCandles.Mappers;

public interface IIndicatorCandleToCandleDetailsMapper
{
    CandleDetails Map(IndicatorCandle candle);
}