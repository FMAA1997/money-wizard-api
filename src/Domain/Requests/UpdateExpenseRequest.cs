namespace Domain.Requests;

public sealed record UpdateExpenseRequest(
    string Description,
    Guid? CategoryId);
