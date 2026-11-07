using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence.Configurations;

public sealed class TreasurerAssignmentConfiguration : IEntityTypeConfiguration<TreasurerAssignment>
{
    public void Configure(EntityTypeBuilder<TreasurerAssignment> builder)
    {
        builder.ToTable("TreasurerAssignment", table =>
            table.HasCheckConstraint(
                "CK_TreasurerAssignment_RevokedTrace",
                """ ("RevokedAt" IS NULL) = ("RevokedBy" IS NULL) """));

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        builder.HasOne<Fund>().WithMany().HasForeignKey(assignment => assignment.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.StudentId, assignment.FundId })
            .HasPrincipalKey(student => new { student.Id, student.FundId })
            .HasConstraintName("FK_TreasurerAssignment_Student_SameFund")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(assignment => assignment.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(assignment => assignment.RevokedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => assignment.StudentId)
            .IsUnique()
            .HasFilter("\"RevokedAt\" IS NULL")
            .HasDatabaseName("UQ_TreasurerAssignment_Student_Active");
        builder.HasIndex(assignment => assignment.FundId).HasDatabaseName("IX_TreasurerAssignment_FundId");
    }
}
