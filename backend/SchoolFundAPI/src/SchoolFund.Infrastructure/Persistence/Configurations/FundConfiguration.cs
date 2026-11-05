using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> builder)
    {
        builder.ToTable("Fund");
        builder.HasKey(fund => fund.Id);
        builder.Property(fund => fund.Id).ValueGeneratedNever();
        builder.Property(fund => fund.Name).HasMaxLength(100);

        builder.HasOne<User>().WithMany().HasForeignKey(fund => fund.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(fund => fund.OwnerUserId).HasDatabaseName("IX_Fund_OwnerUserId");
    }
}
