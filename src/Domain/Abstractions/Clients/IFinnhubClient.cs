namespace Domain.Abstractions.Clients;

public interface IFinnhubClient
{
    Task<decimal?> GetQuote(string symbol, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinnhubSymbol>> SearchSymbols(string query, CancellationToken cancellationToken = default);
}

public sealed record FinnhubSymbol(string Symbol, string Description, string Type);
