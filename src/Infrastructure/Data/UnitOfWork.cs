using System.Data;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Data;

internal sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private readonly ApplicationDbContext _context = context;
    public IDbConnection Connection => _context.Database.GetDbConnection();
    public IDbTransaction? Transaction { get; private set; }

    public void BeginTransaction()
    {
        if (Transaction == null)
        {
            _context.Database.OpenConnection();
            var transaction = _context.Database.BeginTransaction().GetDbTransaction();

            Transaction = transaction;
            _context.Database.UseTransaction(transaction);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken);
        Transaction?.Commit();
        await DisposeTransaction();
    }

    public async Task RollbackAsync()
    {
        Transaction?.Rollback();
        await DisposeTransaction();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransaction();
        _context.Dispose();
    }

    private async Task DisposeTransaction()
    {
        if (Transaction is null)
            return;

        Transaction?.Dispose();
        Transaction = null;
        await _context.Database.CloseConnectionAsync();
    }
}
