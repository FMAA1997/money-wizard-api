namespace Application.DTOs.Investment.Statistics;

public sealed record SavingsRate(
    SavingsRateSnapshot CurrentMonth,
    SavingsRateSnapshot TtmAverage,
    IReadOnlyDictionary<string, decimal> SavingsDifference,
    IReadOnlyDictionary<string, decimal?> SavingsRatePctDifference,
    int Months);

public sealed record SavingsRateSnapshot(
    IReadOnlyDictionary<string, decimal> Income,
    IReadOnlyDictionary<string, decimal> Expenses,
    IReadOnlyDictionary<string, decimal> Savings,
    IReadOnlyDictionary<string, decimal?> SavingsRatePct);
