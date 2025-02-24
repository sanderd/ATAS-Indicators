using ATAS.Indicators;
using sadnerd.io.ATAS.PvsraCandles.Models;

namespace sadnerd.io.ATAS.PvsraCandles.Mappers;

public class IndicatorCandleToCandleDetailsMapper : IIndicatorCandleToCandleDetailsMapper
{
    public CandleDetails Map(IndicatorCandle candle)
    {
        return new CandleDetails(candle.Open, candle.High, candle.Low, candle.Close, candle.Volume);
    }
}