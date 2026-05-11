namespace Application.DTOs.Investment.Statistics;

public sealed record ExpensesCoveredByYield(
    IReadOnlyDictionary<string, decimal> AnnualYield,
    IReadOnlyDictionary<string, decimal> AnnualExpenses,
    IReadOnlyDictionary<string, decimal?> Coverage,
    int Months);
