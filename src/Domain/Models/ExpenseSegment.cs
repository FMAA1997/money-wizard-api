using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ExpenseSegment : Entity
{
    public Guid SeriesId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public RecurrenceRule? RecurrenceRule { get; set; }
    public Guid? PaycheckSeriesId { get; set; }
    public Guid? InvoiceSeriesId { get; set; }

    [JsonIgnore]
    public ExpenseSeries Series { get; set; } = null!;
    public PaycheckSeries? PaycheckSeries { get; set; }
    public InvoiceSeries? InvoiceSeries { get; set; }
}
