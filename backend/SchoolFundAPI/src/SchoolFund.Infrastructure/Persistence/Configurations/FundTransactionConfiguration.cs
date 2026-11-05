using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class FundTransactionConfiguration : IEntityTypeConfiguration<FundTransaction>
{
    public void Configure(EntityTypeBuilder<FundTransaction> builder)
    {
        builder.ToTable("FundTransaction", table =>
        {
            table.HasCheckConstraint("CK_FundTransaction_Type", """ "Type" IN ('Income', 'Expense') """);
            table.HasCheckConstraint("CK_FundTransaction_AmountPositive", """ "Amount" > 0 """);
            table.HasCheckConstraint(
                "CK_FundTransaction_CancelledTrace",
                """ "IsCancelled" = ("CancelledAt" IS NOT NULL) """);
        });

        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(transaction => transaction.Description).HasMaxLength(200);
        builder.Property(transaction => transaction.Note).HasMaxLength(1000);
        builder.Property(transaction => transaction.CancelReason).HasMaxLength(500);

        // Sentinel = true để giá trị false được ghi thật, không bị thay bằng default của cột.
        builder.Property(transaction => transaction.IsPublic).HasDefaultValue(true).HasSentinel(true);

        builder.HasOne<Fund>().WithMany().HasForeignKey(transaction => transaction.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(transaction => new { transaction.CategoryId, transaction.FundId, transaction.Type })
            .HasPrincipalKey(category => new { category.Id, category.FundId, category.Type })
            .HasConstraintName("FK_FundTransaction_Category_SameFundAndType")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(transaction => transaction.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(transaction => transaction.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(transaction => transaction.CancelledBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transaction => new { transaction.FundId, transaction.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UQ_FundTransaction_FundId_IdempotencyKey");
        builder.HasIndex(transaction => new { transaction.FundId, transaction.OccurredOn })
            .HasDatabaseName("IX_FundTransaction_Fund_OccurredOn");
        builder.HasIndex(transaction => new { transaction.FundId, transaction.CreatedAt })
            .HasDatabaseName("IX_FundTransaction_Fund_CreatedAt");
    }
}
