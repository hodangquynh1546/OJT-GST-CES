using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Student");
        builder.HasKey(student => student.Id);
        builder.Property(student => student.Id).ValueGeneratedNever();
        builder.Property(student => student.FullName).HasMaxLength(100);

        builder.HasAlternateKey(student => new { student.Id, student.FundId }).HasName("AK_Student_Id_FundId");

        builder.HasOne<Fund>().WithMany().HasForeignKey(student => student.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(student => student.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(student => student.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(student => student.FundId).HasDatabaseName("IX_Student_FundId");
        builder.HasIndex(student => student.UserId).IsUnique().HasDatabaseName("UQ_Student_UserId");
    }
}
