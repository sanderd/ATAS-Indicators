using sadnerd.io.ATAS.PvsraCandles.Enums;
using sadnerd.io.ATAS.PvsraCandles.Models;

namespace sadnerd.io.ATAS.PvsraCandles.Engines;

public class CandleTypeDeterminator : ICandleTypeDeterminator
{
    public CandleType GetCandleType(CandleDetails currentCandle, CandleDetails[] previousCandles)
    {
        var averagePreviousVolume = previousCandles.Average(c => c.Volume);
        var highestPreviousVolumeSpread = previousCandles.Max(c => c.Volume * (c.High - c.Low));
        var currentVolumeSpread = currentCandle.Volume * (currentCandle.High - currentCandle.Low);

        if (currentCandle.Volume >= 2 * averagePreviousVolume || currentVolumeSpread >= highestPreviousVolumeSpread)
        {
            return currentCandle.Close > currentCandle.Open ? CandleType.Green : CandleType.Red;
        }

        if (currentCandle.Volume >= (decimal)1.5 * averagePreviousVolume)
        {
            return currentCandle.Close > currentCandle.Open ? CandleType.Blue : CandleType.Violet;
        }

        return currentCandle.Close > currentCandle.Open ? CandleType.NeutralPositive : CandleType.NeutralNegative;
    }
}