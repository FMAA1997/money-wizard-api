namespace Domain.Abstractions.Repositories;

public interface IRepository<T> where T : Models.Entity
{
    Task<T?> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAll(CancellationToken cancellationToken = default);
    Task Add(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
