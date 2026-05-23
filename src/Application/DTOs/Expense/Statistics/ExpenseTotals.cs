namespace Application.DTOs.Expense.Statistics;

public class ExpenseTotals
{
    public required IReadOnlyDictionary<string, decimal> TotalYTD { get; set; }
    public required IReadOnlyDictionary<string, decimal> TotalCurrentYear { get; set; }
    public required IReadOnlyDictionary<string, decimal> TotalPreviousYear { get; set; }
    public required IReadOnlyDictionary<string, decimal> VariationYoY { get; set; }
    public required IReadOnlyDictionary<string, decimal> VariationYoyPctg { get; set; }
}
