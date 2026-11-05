using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog", table =>
            table.HasCheckConstraint("CK_AuditLog_Category", """ "Category" IN ('Change', 'Security') """));

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).UseIdentityByDefaultColumn();
        builder.Property(log => log.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(log => log.Action).HasMaxLength(100);
        builder.Property(log => log.EntityType).HasMaxLength(100);
        builder.Property(log => log.EntityId).HasMaxLength(64);
        builder.Property(log => log.OldValues).HasColumnType("jsonb");
        builder.Property(log => log.NewValues).HasColumnType("jsonb");
        builder.Property(log => log.CorrelationId).HasMaxLength(64);
        builder.Property(log => log.IpAddress).HasColumnType("inet");

        builder.HasOne<User>().WithMany().HasForeignKey(log => log.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
