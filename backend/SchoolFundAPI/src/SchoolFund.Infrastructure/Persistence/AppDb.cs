using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Domain.Common;

namespace SchoolFund.Infrastructure.Persistence;

/// <summary>Cài đặt <see cref="IAppDb"/> và <see cref="IDatabaseProbe"/> bằng EF Core.</summary>
public sealed class AppDb(AppDbContext context) : IAppDb, IDatabaseProbe
{
    public IQueryable<T> Query<T>() where T : class => context.Set<T>();

    public void Add<T>(T entity) where T : class => context.Set<T>().Add(entity);

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        EntityFrameworkQueryableExtensions.AnyAsync(query, cancellationToken);

    public async Task<T?> LockAsync<T>(Guid id, CancellationToken cancellationToken) where T : Entity
    {
        var table = context.Model.FindEntityType(typeof(T))!.GetTableName();
        var sql = string.Concat("SELECT * FROM \"", table, "\" WHERE \"Id\" = {0} FOR UPDATE");
        var rows = await context.Set<T>().FromSqlRaw(sql, id).ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    /// <exception cref="UniqueConstraintException">Vi phạm ràng buộc UNIQUE (PostgreSQL 23505).</exception>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgres)
        {
            throw new UniqueConstraintException(postgres.ConstraintName, exception);
        }
    }

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(
            work,
            async (innerWork, token) =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                var result = await innerWork(token);
                await transaction.CommitAsync(token);
                return result;
            },
            cancellationToken);

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);
}
