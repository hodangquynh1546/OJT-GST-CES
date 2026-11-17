using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClassFund.Domain.Entities;

namespace ClassFund.Infrastructure.Persistence.Configurations;

// Quy ước chung: bảng số ít, khóa Guid sinh ở ứng dụng, FK Restrict (không xóa dây chuyền), CHECK cho số tiền.

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.FullName).HasMaxLength(100);
        builder.Property(user => user.PasswordHash).HasMaxLength(500);
        builder.Property(user => user.SecurityStamp).HasMaxLength(64);
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("UX_User_Email");
    }
}

public sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> builder)
    {
        builder.ToTable("Fund");
        builder.HasKey(fund => fund.Id);
        builder.Property(fund => fund.Id).ValueGeneratedNever();
        builder.Property(fund => fund.Name).HasMaxLength(100);
        builder.Property(fund => fund.SchoolYear).HasMaxLength(20);
        builder.HasOne<User>().WithMany().HasForeignKey(fund => fund.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FundMemberConfiguration : IEntityTypeConfiguration<FundMember>
{
    public void Configure(EntityTypeBuilder<FundMember> builder)
    {
        builder.ToTable("FundMember");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.HasOne<Fund>().WithMany().HasForeignKey(member => member.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(member => new { member.FundId, member.UserId }).IsUnique().HasDatabaseName("UX_FundMember_Fund_User");
        builder.HasIndex(member => member.UserId).HasDatabaseName("IX_FundMember_UserId");

        // Mỗi quỹ có đúng một thủ quỹ đầu tiên (Owner) đang hoạt động.
        builder.HasIndex(member => member.FundId)
            .IsUnique()
            .HasFilter("\"Role\" = 'Owner' AND \"Status\" = 'Active'")
            .HasDatabaseName("UX_FundMember_OneOwnerPerFund");
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name).HasMaxLength(100);
        builder.HasOne<Fund>().WithMany().HasForeignKey(category => category.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(category => new { category.FundId, category.Type, category.Name }).IsUnique().HasDatabaseName("UX_Category_Fund_Type_Name");
    }
}

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Event");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(150);
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.Status }).HasDatabaseName("IX_Event_Fund_Status");
    }
}

public sealed class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> builder)
    {
        builder.ToTable("EventParticipant");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Event>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.EventId, item.UserId }).IsUnique().HasDatabaseName("UX_EventParticipant_Event_User");
    }
}

public sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("Charge", table => table.HasCheckConstraint("CK_Charge_Amount", "\"AmountPerPerson\" > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(150);
        builder.Property(item => item.Purpose).HasMaxLength(1000);
        builder.HasOne<Fund>().WithMany().HasForeignKey(item => item.FundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Event>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.FundId, item.EventId, item.Status }).HasDatabaseName("IX_Charge_Fund_Event_Status");
    }
}

public sealed class ChargeAssignmentConfiguration : IEntityTypeConfiguration<ChargeAssignment>
{
    public void Configure(EntityTypeBuilder<ChargeAssignment> builder)
    {
        builder.ToTable("ChargeAssignment", table => table.HasCheckConstraint("CK_ChargeAssignment_Amount", "\"AmountDue\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Charge>().WithMany().HasForeignKey(item => item.ChargeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ChargeId, item.UserId }).IsUnique().HasDatabaseName("UX_ChargeAssignment_Charge_User");
        builder.HasIndex(item => item.UserId).HasDatabaseName("IX_ChargeAssignment_UserId");
    }
}
