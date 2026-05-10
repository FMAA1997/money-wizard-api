using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class InvoiceSeries : Entity
{
    public Guid UserId { get; set; }
    public required string Description { get; set; }
    public InvoiceType Type { get; set; }
    public InvoiceClass? Class { get; set; }
    public int? PointOfSale { get; set; }
    public long? BaseNumber { get; set; }
    public Guid? ParentExceptionId { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    public ICollection<InvoiceSegment> Segments { get; set; } = [];
    public ICollection<InvoiceException> Exceptions { get; set; } = [];
    public InvoiceException? ParentException { get; set; }
}
