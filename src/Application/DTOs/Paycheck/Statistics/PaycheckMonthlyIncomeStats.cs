namespace Application.DTOs.Paycheck.Statistics;

public class PaycheckMonthlyIncomeStats
{
    public required IReadOnlyDictionary<string, decimal> AvgMonthlyIncome { get; set; }
    public required IReadOnlyDictionary<string, decimal> PreviousYearAvgMonthlyIncome { get; set; }
    public required IReadOnlyDictionary<string, decimal> MaxMonthlyIncome { get; set; }
    public required IReadOnlyDictionary<string, decimal> MinMonthlyIncome { get; set; }
    public required IReadOnlyDictionary<string, int> MaxMonthlyIncomeMonth { get; set; }
    public required IReadOnlyDictionary<string, int> MinMonthlyIncomeMonth { get; set; }
}
