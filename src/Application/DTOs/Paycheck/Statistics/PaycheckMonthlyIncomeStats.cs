namespace Application.DTOs.Paycheck.Statistics;
public class PaycheckMonthlyIncomeStats
{
    public decimal AvgMonthlyIncome { get; set; }
    public decimal PreviousYearAvgMonthlyIncome { get; set; }
    public decimal MaxMonthlyIncome { get; set; }
    public decimal MinMonthlyIncome { get; set; }
    public int MaxMonthlyIncomeMonth { get; set; }
    public int MinMonthlyIncomeMonth { get; set; }
}
