using Microsoft.EntityFrameworkCore;

namespace SchoolFund.Infrastructure.Persistence;

/// <summary>DbContext chính. Entity được cấu hình bằng IEntityTypeConfiguration trong assembly này.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
