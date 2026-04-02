namespace Domain.Models;

public sealed class Paycheck : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }

    public User User { get; set; } = null!;
    public ICollection<PaycheckDistribution> Distributions { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
}
