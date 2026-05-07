namespace Application.DTOs.Dashboard;

public class DashboardResults
{
    public DashboardResultPeriod CurrentMonth { get; set; } = new();
    public DashboardResultPeriod YearToDate { get; set; } = new();
    public DashboardResultPeriod YearProjected { get; set; } = new();
}

public class DashboardResultPeriod
{
    public IReadOnlyDictionary<string, decimal> TotalPaychecks { get; set; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> TotalExpenses { get; set; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> PrimaryResult { get; set; } = new Dictionary<string, decimal>();

    public IReadOnlyDictionary<string, decimal>? TotalInvoiced { get; set; }
    public IReadOnlyDictionary<string, decimal>? NonInvoicedTotal { get; set; }
    public IReadOnlyDictionary<string, decimal>? FinalResult { get; set; }
}
