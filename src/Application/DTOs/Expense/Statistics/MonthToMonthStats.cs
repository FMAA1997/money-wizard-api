namespace Application.DTOs.Expense.Statistics;

public class MonthToMonthStats
{
    public decimal CurrentMonth { get; set; }
    public decimal PreviousMonth { get; set; }
    public decimal Variation { get; set; }
    public decimal VariationPctg { get; set; }
    public required string CurrentMonthLabel { get; set; }
    public required string PreviousMonthLabel { get; set; }
}
