namespace Application.DTOs.Argentina.Macro;

public class MacroSummary
{
    public required RiesgoPais RiesgoPais { get; set; }
    public required InflationPoint Mom { get; set; }
    public required InflationPoint Yoy { get; set; }
}

public class RiesgoPais
{
    public decimal Valor { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal? AbsoluteChangeBps { get; set; }
    public decimal? PercentChange { get; set; }
    public DateOnly? PreviousFecha { get; set; }
}

public class InflationPoint
{
    public decimal Latest { get; set; }
    public DateOnly LatestFecha { get; set; }
    public decimal? Previous { get; set; }
    public DateOnly? PreviousFecha { get; set; }
}
