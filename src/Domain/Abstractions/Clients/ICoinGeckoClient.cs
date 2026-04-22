namespace Domain.Abstractions.Clients;

public interface ICoinGeckoClient
{
    Task<IReadOnlyDictionary<string, decimal>> GetUsdPrices(IEnumerable<string> coinIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoinGeckoCoin>> ListCoins(CancellationToken cancellationToken = default);
}

public sealed record CoinGeckoCoin(string Id, string Symbol, string Name);
