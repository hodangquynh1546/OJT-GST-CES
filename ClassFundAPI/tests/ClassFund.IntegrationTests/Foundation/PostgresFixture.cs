using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using ClassFund.Infrastructure.Persistence;

namespace ClassFund.IntegrationTests.Foundation;

/// <summary>
/// PostgreSQL tạm trong Docker cho integration test (KHÔNG dùng Neon dùng chung để chạy test). Cần Docker Desktop đang chạy.
/// Schema tạo bằng chính các migration (giống Neon), nên test bắt được cả trigger / CHECK / index viết trong migration.
/// Dùng: <c>public class XTests(PostgresFixture db) : IClassFixture&lt;PostgresFixture&gt;</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
