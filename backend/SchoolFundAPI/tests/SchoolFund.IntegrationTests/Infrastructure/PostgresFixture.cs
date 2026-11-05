using Microsoft.EntityFrameworkCore;
using SchoolFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SchoolFund.IntegrationTests.Infrastructure;

/// <summary>Một PostgreSQL 16 (Docker) cho cả bộ test: áp toàn bộ migration một lần, dùng chung một <see cref="ApiFactory"/>.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public ApiFactory Factory { get; private set; } = null!;

    public TestData Data { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();

        Factory = new ApiFactory(ConnectionString);
        Data = new TestData(Factory);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "api";
}
