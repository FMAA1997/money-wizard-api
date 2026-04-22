namespace Domain.Abstractions.Clients;

public interface IData912Client
{
    Task<IReadOnlyList<Data912Quote>> GetUsStocks(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Data912Quote>> GetArgStocks(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Data912Quote>> GetArgCedears(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Data912Quote>> GetArgBonds(CancellationToken cancellationToken = default);
}

public sealed record Data912Quote(string Symbol, decimal Price, decimal? PctChange);
