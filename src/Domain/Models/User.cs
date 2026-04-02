namespace Domain.Models;

public sealed class User : Entity
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public DateOnly Dob { get; set; }

    public ICollection<Paycheck> Paychecks { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = [];
}
