using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class PaycheckException : Entity
{
    public Guid SeriesId { get; set; }
    public DateOnly OriginalDate { get; set; }
    public DateOnly? Date { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public bool IsDeleted { get; set; }

    [JsonIgnore]
    public PaycheckSeries Series { get; set; } = null!;
}
