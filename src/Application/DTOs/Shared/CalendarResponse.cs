namespace Application.DTOs.Shared;
public record CalendarResponse<T>(
    List<string> Months,
    List<T> Rows,
    List<decimal> Totals
);
