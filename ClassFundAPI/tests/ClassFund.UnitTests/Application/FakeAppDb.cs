using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Common;

namespace ClassFund.UnitTests.Application;

/// <summary>IAppDb trong bộ nhớ cho unit test service (không cần EF Core / PostgreSQL).</summary>
public sealed class FakeAppDb : IAppDb
{
    private readonly Dictionary<Type, List<object>> _sets = [];

    public void Seed<T>(params T[] entities) where T : class => Set<T>().AddRange(entities);

    public IQueryable<T> Query<T>() where T : class => Set<T>().Cast<T>().AsQueryable();

    public void Add<T>(T entity) where T : class => Set<T>().Add(entity);

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.ToList());

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.FirstOrDefault());

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Count());

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Any());

    public Task<T?> LockAsync<T>(Guid id, CancellationToken cancellationToken) where T : Entity =>
        Task.FromResult(Set<T>().Cast<T>().FirstOrDefault(item => item.Id == id));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) => work(cancellationToken);

    private List<object> Set<T>() => _sets.TryGetValue(typeof(T), out var list) ? list : _sets[typeof(T)] = [];
}
