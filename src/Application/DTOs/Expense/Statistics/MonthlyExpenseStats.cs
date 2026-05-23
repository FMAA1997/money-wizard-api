namespace Application.DTOs.Expense.Statistics;

public class MonthlyExpenseStats
{
    public required IReadOnlyDictionary<string, decimal> AvgMonthlyExpense { get; set; }
    public required IReadOnlyDictionary<string, decimal> PreviousYearAvgMonthlyExpense { get; set; }
    public required IReadOnlyDictionary<string, decimal> MaxMonthlyExpense { get; set; }
    public required IReadOnlyDictionary<string, decimal> MinMonthlyExpense { get; set; }
    public required IReadOnlyDictionary<string, int> MaxMonthlyExpenseMonth { get; set; }
    public required IReadOnlyDictionary<string, int> MinMonthlyExpenseMonth { get; set; }
}
