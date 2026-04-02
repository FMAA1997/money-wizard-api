namespace Domain.Models;

public sealed class PaycheckDistribution : Entity
{
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public Guid? Source { get; set; }

    public Paycheck? Paycheck { get; set; }
}
