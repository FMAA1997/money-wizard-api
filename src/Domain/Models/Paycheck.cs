using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class Paycheck : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }

    // Recurrence
    public RecurrenceRule? RecurrenceRule { get; set; }

    // Override/exception support
    public Guid? RecurringPaycheckId { get; set; }
    public DateOnly? OriginalDate { get; set; }
    public bool IsDeleted { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public ICollection<Invoice> Invoices { get; set; } = [];
    [JsonIgnore]
    public ICollection<Expense> Expenses { get; set; } = [];
    [JsonIgnore]
    public Paycheck? RecurringPaycheck { get; set; }
    [JsonIgnore]
    public ICollection<Paycheck> Exceptions { get; set; } = [];
}
