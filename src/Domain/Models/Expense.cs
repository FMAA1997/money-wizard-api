namespace Domain.Models;

public sealed class Expense : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? Source { get; set; }

    public User User { get; set; } = null!;
    public ExpenseCategory? Category { get; set; }
    public Paycheck? Paycheck { get; set; }
}
