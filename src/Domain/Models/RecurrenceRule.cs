namespace Domain.Models;

public sealed class RecurrenceRule
{
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public DateOnly? EndDate { get; set; }
    public int? TotalInstallments { get; set; }
}
