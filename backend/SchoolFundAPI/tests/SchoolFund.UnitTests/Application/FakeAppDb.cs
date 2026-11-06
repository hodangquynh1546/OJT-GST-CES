using SchoolFund.Application.Common.Persistence;
using SchoolFund.Domain.Common;

namespace SchoolFund.UnitTests.Application;

/// <summary>IAppDb trong bộ nhớ: truy vấn LINQ của service chạy trên danh sách thật.</summary>
internal sealed class FakeAppDb : IAppDb
{
    private readonly Dictionary<Type, List<object>> _sets = [];

    /// <summary>Giả lập database từ chối lần ghi.</summary>
    public Exception? SaveFailure { get; set; }

    public IQueryable<T> Query<T>() where T : class =>
        (_sets.TryGetValue(typeof(T), out var items) ? items.Cast<T>() : []).AsQueryable();

    public void Add<T>(T entity) where T : class
    {
        if (!_sets.TryGetValue(typeof(T), out var items))
        {
            _sets[typeof(T)] = items = [];
        }

        items.Add(entity);
    }

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.ToList());

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.FirstOrDefault());

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.Count());

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.Any());

    public Task<T?> LockAsync<T>(Guid id, CancellationToken cancellationToken) where T : Entity =>
        Task.FromResult(Query<T>().FirstOrDefault(entity => entity.Id == id));

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveFailure is null ? Task.CompletedTask : Task.FromException(SaveFailure);

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        work(cancellationToken);
}
