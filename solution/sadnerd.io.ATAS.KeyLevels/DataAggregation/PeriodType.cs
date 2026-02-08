namespace sadnerd.io.ATAS.KeyLevels.DataAggregation;

/// <summary>
/// Defines the type of time period for key level calculations.
/// </summary>
public enum PeriodType
{
    /// <summary>4-hour trading period</summary>
    FourHour,
    
    /// <summary>Daily trading session</summary>
    Daily,
    
    /// <summary>Monday of the week (used for weekly range reference)</summary>
    Monday,
    
    /// <summary>Full trading week (Monday-Friday)</summary>
    Weekly,
    
    /// <summary>Calendar month</summary>
    Monthly,
    
    /// <summary>Calendar quarter (Q1, Q2, Q3, Q4)</summary>
    Quarterly,
    
    /// <summary>Calendar year</summary>
    Yearly
}
