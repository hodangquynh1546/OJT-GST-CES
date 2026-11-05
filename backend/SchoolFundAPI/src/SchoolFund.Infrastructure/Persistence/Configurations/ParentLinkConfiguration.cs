using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolFund.Domain.Entities;

namespace SchoolFund.Infrastructure.Persistence.Configurations;

public sealed class ParentLinkConfiguration : IEntityTypeConfiguration<ParentLink>
{
    public void Configure(EntityTypeBuilder<ParentLink> builder)
    {
        builder.ToTable("ParentLink", table =>
            table.HasCheckConstraint("CK_ParentLink_RemovedTrace", """ "IsActive" = ("RemovedAt" IS NULL) """));

        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedNever();

        builder.HasOne<Student>().WithMany().HasForeignKey(link => link.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(link => link.ParentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(link => link.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(link => link.RemovedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => link.StudentId)
            .IsUnique()
            .HasFilter("\"IsActive\"")
            .HasDatabaseName("UQ_ParentLink_Student_Active");
        builder.HasIndex(link => link.ParentUserId).HasDatabaseName("IX_ParentLink_ParentUserId");
    }
}
