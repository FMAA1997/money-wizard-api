namespace Application.DTOs.Argentina.Holidays;

public class UpcomingHolidaysResponse
{
    public List<Holiday> Holidays { get; set; } = [];
}

public class Holiday
{
    public DateOnly Fecha { get; set; }
    public required string Tipo { get; set; }
    public required string Nombre { get; set; }
}
