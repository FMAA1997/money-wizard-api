using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class Expense : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? PaycheckId { get; set; }
    public Guid? InvoiceId { get; set; }

    // Recurrence
    public RecurrenceRule? RecurrenceRule { get; set; }

    // Override/exception support
    public Guid? RecurringExpenseId { get; set; }
    public DateOnly? OriginalDate { get; set; }
    public bool IsDeleted { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    public ExpenseCategory? Category { get; set; }
    public Paycheck? Paycheck { get; set; }
    public Invoice? Invoice { get; set; }
    [JsonIgnore]
    public Expense? RecurringExpense { get; set; }
    [JsonIgnore]
    public ICollection<Expense> Exceptions { get; set; } = [];
}
