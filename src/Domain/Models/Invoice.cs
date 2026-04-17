using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class Invoice : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public Guid? Source { get; set; }

    // Recurrence
    public RecurrenceRule? RecurrenceRule { get; set; }

    // Override/exception support
    public Guid? RecurringInvoiceId { get; set; }
    public DateOnly? OriginalDate { get; set; }
    public bool IsDeleted { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    public Paycheck? Paycheck { get; set; }
    [JsonIgnore]
    public Invoice? RecurringInvoice { get; set; }
    [JsonIgnore]
    public ICollection<Invoice> Exceptions { get; set; } = [];
    [JsonIgnore]
    public ICollection<Expense> Expenses { get; set; } = [];
}
