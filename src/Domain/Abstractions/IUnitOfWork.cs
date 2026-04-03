using System.Data;

namespace Domain.Abstractions;

public interface IUnitOfWork : IAsyncDisposable
{
    public IDbConnection Connection { get; }
    public IDbTransaction? Transaction { get; }
    public void BeginTransaction();
    public Task CommitAsync(CancellationToken cancellationToken = default);
    public Task RollbackAsync();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
