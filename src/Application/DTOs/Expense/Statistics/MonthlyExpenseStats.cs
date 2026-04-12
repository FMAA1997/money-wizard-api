namespace Application.DTOs.Expense.Statistics;

public class MonthlyExpenseStats
{
    public decimal AvgMonthlyExpense { get; set; }
    public decimal PreviousYearAvgMonthlyExpense { get; set; }
    public decimal MaxMonthlyExpense { get; set; }
    public decimal MinMonthlyExpense { get; set; }
    public int MaxMonthlyExpenseMonth { get; set; }
    public int MinMonthlyExpenseMonth { get; set; }
}
