using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ExpenseSeries : Entity
{
    public Guid UserId { get; set; }
    public required string Description { get; set; }
    public Guid? CategoryId { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    public ExpenseCategory? Category { get; set; }
    public ICollection<ExpenseSegment> Segments { get; set; } = [];
    public ICollection<ExpenseException> Exceptions { get; set; } = [];
}
