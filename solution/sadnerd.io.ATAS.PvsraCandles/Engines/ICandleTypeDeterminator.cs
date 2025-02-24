using sadnerd.io.ATAS.PvsraCandles.Enums;
using sadnerd.io.ATAS.PvsraCandles.Models;

namespace sadnerd.io.ATAS.PvsraCandles.Engines;

public interface ICandleTypeDeterminator
{
    CandleType GetCandleType(CandleDetails currentCandle, CandleDetails[] previousCandles);
}