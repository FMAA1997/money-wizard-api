namespace Domain.Models;

public sealed class InvoiceCategory : Entity
{
    public required string Name { get; set; }
    public required string Country { get; set; }
    public required string Type { get; set; }
    public DateOnly CutDate { get; set; }
    public decimal Bottom { get; set; }
    public decimal Top { get; set; }
    public decimal Tax { get; set; }
}
