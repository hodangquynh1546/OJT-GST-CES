using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence;

/// <summary>
/// DbContext chính. Entity cấu hình bằng IEntityTypeConfiguration trong thư mục Configurations.
/// Quy ước: enum lưu dạng chuỗi (đọc được khi đối chiếu), tiền là bigint (VND), thời điểm là timestamptz.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Fund> Funds => Set<Fund>();

    public DbSet<FundMember> FundMembers => Set<FundMember>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();

    public DbSet<Charge> Charges => Set<Charge>();

    public DbSet<ChargeAssignment> ChargeAssignments => Set<ChargeAssignment>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();

    public DbSet<PersonalBalance> PersonalBalances => Set<PersonalBalance>();

    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();

    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<PaymentReminder> PaymentReminders => Set<PaymentReminder>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<string>().HaveMaxLength(500);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
