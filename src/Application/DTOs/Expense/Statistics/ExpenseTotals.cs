namespace Application.DTOs.Expense.Statistics;

public class ExpenseTotals
{
    public decimal TotalYTD { get; set; }
    public decimal TotalCurrentYear { get; set; }
    public decimal TotalPreviousYear { get; set; }
    public decimal VariationYoY { get; set; }
    public decimal VariationYoyPctg { get; set; }
}
