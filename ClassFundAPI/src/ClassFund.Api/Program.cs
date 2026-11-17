using ClassFund.Api.Hosting;
using ClassFund.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.AddClassFundServices();

var app = builder.Build();

// Seed dữ liệu giả cho môi trường phát triển – chỉ chạy khi gọi rõ ràng, xong thì thoát (không khởi động web).
if (args.Contains("--seed"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--seed chỉ chạy ở môi trường Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var created = await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);
    Console.WriteLine($"Seed xong: tạo mới {created} nhóm dữ liệu.");
    return;
}

app.UseClassFundPipeline();

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
