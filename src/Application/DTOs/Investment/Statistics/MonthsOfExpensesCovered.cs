namespace Application.DTOs.Investment.Statistics;

public sealed record MonthsOfExpensesCovered(
    IReadOnlyDictionary<string, decimal?> MonthsCovered,
    IReadOnlyDictionary<string, decimal> TotalInvestments,
    IReadOnlyDictionary<string, decimal> AvgMonthlyExpenses,
    int Months);
