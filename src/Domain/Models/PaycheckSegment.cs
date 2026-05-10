using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class PaycheckSegment : Entity
{
    public Guid SeriesId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public RecurrenceRule? RecurrenceRule { get; set; }

    [JsonIgnore]
    public PaycheckSeries Series { get; set; } = null!;
}
