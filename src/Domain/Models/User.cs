using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class User : Entity
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public DateOnly Dob { get; set; }
    public string Country { get; set; } = "row";

    [JsonIgnore]
    public ICollection<Paycheck> Paychecks { get; set; } = [];
    [JsonIgnore]
    public ICollection<Invoice> Invoices { get; set; } = [];
    [JsonIgnore]
    public ICollection<Expense> Expenses { get; set; } = [];
    [JsonIgnore]
    public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = [];
    [JsonIgnore]
    public ArgentinaUserProfile? ArgentinaProfile { get; set; }
}
