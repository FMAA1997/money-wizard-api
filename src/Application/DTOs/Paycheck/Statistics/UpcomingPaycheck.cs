namespace Application.DTOs.Paycheck.Statistics;

public class UpcomingPaycheck
{
    public DateOnly Date { get; set; }
    public int RemainingDays { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
}
