namespace sadnerd.io.ATAS.PvsraCandles.Models;

public record VectorShadow(
    int StartBar, 
    decimal InitialPriceLow, 
    decimal InitialPriceHigh, 
    int? EndBar, 
    decimal UnrecoveredPriceLow,
    decimal UnrecoveredPriceHigh
)
{
    public int? EndBar { get; set; } = EndBar;
    public decimal UnrecoveredPriceLow { get; set; } = UnrecoveredPriceLow;
    public decimal UnrecoveredPriceHigh { get; set; } = UnrecoveredPriceHigh;
}