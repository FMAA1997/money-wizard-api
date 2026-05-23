namespace Application.DTOs.Paycheck.Statistics;

public class PaycheckTotals
{
    public required IReadOnlyDictionary<string, decimal> TotalYTD { get; set; }
    public required IReadOnlyDictionary<string, decimal> TotalCurrentYear { get; set; }
    public required IReadOnlyDictionary<string, decimal> TotalPreviousYear { get; set; }
    public required IReadOnlyDictionary<string, decimal> VariationYoY { get; set; }
    public required IReadOnlyDictionary<string, decimal> VariationYoyPctg { get; set; }
}
