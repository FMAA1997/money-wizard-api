namespace Application.DTOs.Investment.Statistics;

public sealed record FinancialIndependence(
    IReadOnlyDictionary<string, decimal> AnnualYield,
    FinancialIndependenceMilestone Savings,
    FinancialIndependenceMilestone Expenses,
    FinancialIndependenceMilestone Income,
    int Months);

public sealed record FinancialIndependenceMilestone(
    IReadOnlyDictionary<string, decimal> Current,
    IReadOnlyDictionary<string, decimal> Target,
    IReadOnlyDictionary<string, decimal?> Coverage,
    IReadOnlyDictionary<string, decimal?> RequiredInvestments);
