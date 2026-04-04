using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class Paycheck : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public ICollection<PaycheckDistribution> Distributions { get; set; } = [];
    [JsonIgnore]
    public ICollection<Expense> Expenses { get; set; } = [];
}
