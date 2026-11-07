using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence;

/// <summary>DbContext chính. Entity được cấu hình bằng IEntityTypeConfiguration trong assembly này.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Fund> Funds => Set<Fund>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<ParentLink> ParentLinks => Set<ParentLink>();

    public DbSet<TreasurerAssignment> TreasurerAssignments => Set<TreasurerAssignment>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FundTransaction> FundTransactions => Set<FundTransaction>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
