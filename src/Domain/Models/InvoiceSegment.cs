using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class InvoiceSegment : Entity
{
    public Guid SeriesId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public RecurrenceRule? RecurrenceRule { get; set; }
    public Guid? Source { get; set; }

    [JsonIgnore]
    public InvoiceSeries Series { get; set; } = null!;
    public PaycheckSeries? PaycheckSeries { get; set; }
}
