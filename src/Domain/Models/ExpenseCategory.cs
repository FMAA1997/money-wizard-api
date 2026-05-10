using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ExpenseCategory : Entity
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public ICollection<ExpenseSeries> ExpenseSeries { get; set; } = [];
}
