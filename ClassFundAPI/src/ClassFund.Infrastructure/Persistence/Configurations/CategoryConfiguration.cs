using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence.Configurations;

/// <summary>UQ_Category_Fund_Type_Name dùng lower(Name) nên được tạo bằng SQL trong migration AddDatabaseProtections.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category", table =>
            table.HasCheckConstraint("CK_Category_Type", """ "Type" IN ('Income', 'Expense') """));

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(category => category.Name).HasMaxLength(100);

        builder.HasAlternateKey(category => new { category.Id, category.FundId, category.Type })
            .HasName("AK_Category_Id_FundId_Type");

        builder.HasOne<Fund>().WithMany().HasForeignKey(category => category.FundId).OnDelete(DeleteBehavior.Restrict);
    }
}
