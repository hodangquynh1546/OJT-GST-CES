using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User", table =>
        {
            table.HasCheckConstraint("CK_User_Kind", """ "Kind" IN ('Teacher', 'Student', 'Parent') """);
            table.HasCheckConstraint("CK_User_Status", """ "Status" IN ('Active', 'Disabled') """);
            table.HasCheckConstraint("CK_User_LoginName", """ "NormalizedEmail" IS NOT NULL OR "Username" IS NOT NULL """);
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254);
        builder.Property(user => user.Username).HasMaxLength(60);
        builder.Property(user => user.FullName).HasMaxLength(200);
        builder.Property(user => user.PasswordHash).HasMaxLength(500);
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(user => user.MustChangePassword).HasDefaultValue(false);
        builder.Property(user => user.SecurityStamp).HasMaxLength(64);

        builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("UQ_User_NormalizedEmail");
        builder.HasIndex(user => user.Username).IsUnique().HasDatabaseName("UQ_User_Username");
    }
}
