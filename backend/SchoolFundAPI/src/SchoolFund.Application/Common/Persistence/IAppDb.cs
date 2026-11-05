namespace SchoolFund.Application.Common.Persistence;

/// <summary>Port truy cập dữ liệu: Application dùng IQueryable mà không phụ thuộc EF Core.</summary>
public interface IAppDb
{
    IQueryable<T> Query<T>() where T : class;

    void Add<T>(T entity) where T : class;

    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Chạy <paramref name="work"/> trong một transaction. Lỗi tạm thời sẽ chạy lại toàn bộ, nên mọi thay đổi phải nằm trong <paramref name="work"/>.</summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
