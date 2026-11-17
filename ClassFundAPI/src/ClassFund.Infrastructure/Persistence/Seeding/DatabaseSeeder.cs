using Microsoft.EntityFrameworkCore;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Infrastructure.Persistence.Seeding;

/// <summary>
/// Dữ liệu seed dùng chung cho hai developer (yêu cầu 10.4). Idempotent: chạy lại không tạo trùng (khóa theo email / tên quỹ).
/// Chỉ chạy khi được yêu cầu rõ ràng (<c>dotnet run --project src/ClassFund.Api -- --seed</c>), không bao giờ tự chạy khi khởi động.
/// Chỉ tạo dữ liệu GIẢ; không xóa hay reset dữ liệu có sẵn.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
{
    public const string DemoFundName = "Lớp 10A1 (demo)";
    public const string DemoOwnerEmail = "thuquy.demo@classfund.local";
    public static readonly string[] DemoStudentEmails =
        ["hocsinh1.demo@classfund.local", "hocsinh2.demo@classfund.local", "hocsinh3.demo@classfund.local"];

    /// <summary>Mật khẩu của tài khoản demo (dữ liệu giả, chỉ cho môi trường phát triển).</summary>
    public const string DemoPassword = "Demo1234";

    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var created = 0;

        var owner = await EnsureUserAsync(DemoOwnerEmail, "Thủ quỹ Demo", UserOrigin.SelfRegistered, false, now, cancellationToken);
        var fund = await db.Funds.FirstOrDefaultAsync(item => item.CreatedByUserId == owner.Id && item.Name == DemoFundName, cancellationToken);
        if (fund is null)
        {
            fund = new Fund(DemoFundName, owner.Id, "2026-2027", now);
            db.Funds.Add(fund);
            db.FundMembers.Add(new FundMember(fund.Id, owner.Id, MemberRole.Owner, now));
            foreach (var (name, type) in DefaultCategories.All)
            {
                db.Categories.Add(new Category(fund.Id, name, type, isSystem: true));
            }

            created++;
        }

        foreach (var (email, index) in DemoStudentEmails.Select((email, index) => (email, index)))
        {
            var student = await EnsureUserAsync(email, $"Học sinh Demo {index + 1}", UserOrigin.Provisioned, true, now, cancellationToken);
            var isMember = await db.FundMembers.AnyAsync(item => item.FundId == fund.Id && item.UserId == student.Id, cancellationToken);
            if (!isMember)
            {
                db.FundMembers.Add(new FundMember(fund.Id, student.Id, MemberRole.Student, now));
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<User> EnsureUserAsync(string email, string fullName, UserOrigin origin, bool mustChangePassword, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var user = new User(email, fullName, hasher.Hash(DemoPassword), origin, mustChangePassword, now);
        db.Users.Add(user);
        return user;
    }
}
