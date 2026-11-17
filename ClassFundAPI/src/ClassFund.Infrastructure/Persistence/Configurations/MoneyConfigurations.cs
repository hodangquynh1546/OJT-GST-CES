using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment", table => table.HasCheckConstraint("CK_Payment_Amount", "\"Amount\" > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TransferCode).HasMaxLength(100);
        builder.Property(item => item.RejectionReason).HasMaxLength(500);
        builder.HasOne<ChargeAssignment>().WithMany().HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.PayerUserId).OnDelete(DeleteBehavior.Restrict);

        // Một nghĩa vụ chỉ có một lần nộp đang chờ/đã xác nhận; bị từ chối thì được nộp lại.
        builder.HasIndex(item => item.AssignmentId)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Rejected'")
            .HasDatabaseName("UX_Payment_OneActivePerAssignment");
        builder.HasIndex(item => new { item.PayerUserId, item.SubmittedAt }).HasDatabaseName("IX_Payment_Payer_SubmittedAt");
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntry", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntry_Amount", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_LedgerEntry_NotSelfReversal", "\"ReversesEntryId\" IS NULL OR \"ReversesEntryId\" <> \"Id\"");
        });

        // Bút toán đảo trỏ về dòng gốc; một dòng chỉ bị đảo một lần (BR-08).
        builder.HasOne<LedgerEntry>().WithMany().HasForeignKey(item => item.ReversesEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ReversesEntryId).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL").HasDatabaseName("UX_LedgerEntry_ReversesEntryId");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(150);
        builder.Property(item => item.Purpose).HasMaxLength(1000);
        builder.Property(item => item.TransferCode).HasMaxLength(100);
        builder.Property(item => item.Note).HasMaxLength(1000);
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Event>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.EventId, item.TransactionDate }).HasDatabaseName("IX_LedgerEntry_Fund_Event_Date");
        builder.HasIndex(item => item.PaymentId).IsUnique().HasFilter("\"PaymentId\" IS NOT NULL").HasDatabaseName("UX_LedgerEntry_PaymentId");
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachment");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.FileName).HasMaxLength(255);
        builder.Property(item => item.ContentType).HasMaxLength(100);
        builder.Property(item => item.StorageKey).HasMaxLength(300);
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.OwnerId).HasDatabaseName("IX_Attachment_OwnerId");
        builder.HasIndex(item => item.StorageKey).IsUnique().HasDatabaseName("UX_Attachment_StorageKey");

        // Chỉ ảnh đại diện được phép không thuộc quỹ.
        builder.ToTable(table => table.HasCheckConstraint("CK_Attachment_FundRequired", "\"FundId\" IS NOT NULL OR \"Kind\" = 'Avatar'"));
    }
}

public sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlement");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Ignore(item => item.Difference);
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Event>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);

        // Mỗi sự kiện quyết toán một lần; mỗi quỹ tổng kết cuối năm (EventId null) một lần.
        builder.HasIndex(item => item.EventId).IsUnique().HasFilter("\"EventId\" IS NOT NULL").HasDatabaseName("UX_Settlement_EventId");
        builder.HasIndex(item => item.FundId).IsUnique().HasFilter("\"EventId\" IS NULL").HasDatabaseName("UX_Settlement_OneYearEndPerFund");
    }
}

public sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> builder)
    {
        builder.ToTable("SettlementLine", table => table.HasCheckConstraint("CK_SettlementLine_Amount", "\"RefundAmount\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Settlement>().WithMany().HasForeignKey(item => item.SettlementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.SettlementId, item.UserId }).IsUnique().HasDatabaseName("UX_SettlementLine_Settlement_User");
    }
}

public sealed class PersonalBalanceConfiguration : IEntityTypeConfiguration<PersonalBalance>
{
    public void Configure(EntityTypeBuilder<PersonalBalance> builder)
    {
        builder.ToTable("PersonalBalance", table => table.HasCheckConstraint("CK_PersonalBalance_NonNegative", "\"Balance\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        // xmin của PostgreSQL làm token đồng thời: hai request trừ/cộng cùng lúc thì một request phải thử lại.
        builder.Property(item => item.Version).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.UserId }).IsUnique().HasDatabaseName("UX_PersonalBalance_Fund_User");
    }
}

public sealed class BalanceTransactionConfiguration : IEntityTypeConfiguration<BalanceTransaction>
{
    public void Configure(EntityTypeBuilder<BalanceTransaction> builder)
    {
        builder.ToTable("BalanceTransaction", table => table.HasCheckConstraint("CK_BalanceTransaction_AfterNonNegative", "\"BalanceAfter\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.UserId, item.CreatedAt }).HasDatabaseName("IX_BalanceTransaction_Fund_User_At");
    }
}

public sealed class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable("WithdrawalRequest", table => table.HasCheckConstraint("CK_WithdrawalRequest_Amount", "\"Amount\" > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.Status }).HasDatabaseName("IX_WithdrawalRequest_Fund_Status");

        // Rút toàn bộ một lần: mỗi người chỉ một yêu cầu đang chờ trong một quỹ (OQ-11).
        builder.HasIndex(item => new { item.FundId, item.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Requested'")
            .HasDatabaseName("UX_WithdrawalRequest_OnePendingPerUser");
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", table => table.HasCheckConstraint("CK_Notification_Scope", "(\"Scope\" = 'Event') = (\"EventId\" IS NOT NULL)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(200);
        builder.Property(item => item.Body).HasMaxLength(1000);
        builder.Property(item => item.ReferenceType).HasMaxLength(50);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.IsRead, item.CreatedAt }).HasDatabaseName("IX_Notification_User_Read_At");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog", table => table.HasCheckConstraint("CK_AuditLog_Scope", "(\"Scope\" = 'Event') = (\"EventId\" IS NOT NULL)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Action).HasMaxLength(100);
        builder.Property(item => item.EntityType).HasMaxLength(100);
        builder.Property(item => item.EntityId).HasMaxLength(64);
        builder.Property(item => item.BeforeJson).HasColumnType("jsonb");
        builder.Property(item => item.AfterJson).HasColumnType("jsonb");
        builder.Property(item => item.Note).HasMaxLength(1000);
        builder.Property(item => item.CorrelationId).HasMaxLength(64);
        builder.HasIndex(item => new { item.FundId, item.At }).HasDatabaseName("IX_AuditLog_Fund_At");
        builder.HasIndex(item => new { item.FundId, item.Scope, item.EventId }).HasDatabaseName("IX_AuditLog_Fund_Scope_Event");
    }
}
