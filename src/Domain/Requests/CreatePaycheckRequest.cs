namespace Domain.Requests;

public sealed record CreatePaycheckRequest(DateOnly Date, decimal Amount, string Description);
