namespace Application.DTOs.Argentina.Dollars;

public class DollarRatesResponse
{
    public List<DollarRate> Rates { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}

public class DollarRate
{
    public required string Casa { get; set; }
    public required string Nombre { get; set; }
    public required string Moneda { get; set; }
    public decimal Compra { get; set; }
    public decimal Venta { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }
    public decimal? Variacion { get; set; }
}
