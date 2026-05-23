namespace Application.DTOs.Expense.Statistics;

public class MonthToMonthStats
{
    public required IReadOnlyDictionary<string, decimal> CurrentMonth { get; set; }
    public required IReadOnlyDictionary<string, decimal> PreviousMonth { get; set; }
    public required IReadOnlyDictionary<string, decimal> Variation { get; set; }
    public required IReadOnlyDictionary<string, decimal> VariationPctg { get; set; }
    public required string CurrentMonthLabel { get; set; }
    public required string PreviousMonthLabel { get; set; }
}
