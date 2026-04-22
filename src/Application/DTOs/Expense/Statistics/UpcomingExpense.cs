namespace Application.DTOs.Expense.Statistics;

public class UpcomingExpense
{
    public DateOnly Date { get; set; }
    public int RemainingDays { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required IReadOnlyDictionary<string, decimal> Amounts { get; set; }
}
