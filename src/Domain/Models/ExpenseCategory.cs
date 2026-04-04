using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ExpenseCategory : Entity
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public ICollection<Expense> Expenses { get; set; } = [];
}
