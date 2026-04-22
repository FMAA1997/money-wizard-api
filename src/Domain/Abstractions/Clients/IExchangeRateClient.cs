namespace Domain.Abstractions.Clients;

public interface IExchangeRateClient
{
    Task<IReadOnlyList<ExchangeRateEntry>> GetMepHistoryAsync(CancellationToken cancellationToken = default);
}

public sealed record ExchangeRateEntry(DateOnly Date, decimal UsdToArs);
