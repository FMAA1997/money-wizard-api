using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ExpenseException : Entity
{
    public Guid SeriesId { get; set; }
    public DateOnly OriginalDate { get; set; }
    public DateOnly? Date { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public bool IsDeleted { get; set; }

    [JsonIgnore]
    public ExpenseSeries Series { get; set; } = null!;
}
