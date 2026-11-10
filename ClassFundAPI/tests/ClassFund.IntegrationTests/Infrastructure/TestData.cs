using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>Seed trực tiếp vào DB những dữ liệu mà feature tương ứng chưa có API (quỹ, học sinh, phụ huynh, thủ quỹ).</summary>
public sealed class TestData(ApiFactory factory)
{
    public const string TemporaryPassword = "Tam-1234-abcd-5678";

    public static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 10)];

    public static string NewEmail() => $"{Unique("u")}@example.vn";

    public async Task<SeededFund> CreateFundAsync(Guid? ownerUserId = null, string? name = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var ownerId = ownerUserId;
        if (ownerId is null)
        {
            var owner = NewUser(scope, UserKind.Teacher, "Cô Hạnh", TeacherPassword, NewEmail(), null, false);
            db.Add(owner);
            ownerId = owner.Id;
        }

        var fund = new Fund(ownerId.Value, name ?? Unique("Quỹ lớp "), now);
        db.Add(fund);
        await db.SaveChangesAsync();
        return new SeededFund(fund.Id, fund.Name, ownerId.Value);
    }

    public const string TeacherPassword = "Matkhau123";

    /// <summary>
    /// Giáo viên (GVCN) tự đăng ký, <b>chưa có quỹ</b>: đã đặt mật khẩu (không còn mật khẩu tạm), đang hoạt động.
    /// Đăng nhập trong test bằng <c>client.SignInAsAsync(teacher.UserId)</c>, hoặc bằng <c>Login</c> / <c>Password</c> khi đã có API đăng nhập (M1).
    /// </summary>
    public async Task<SeededAccount> CreateTeacherAsync(string fullName = "Cô Hạnh", string password = TeacherPassword)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = NewEmail();
        var teacher = NewUser(scope, UserKind.Teacher, fullName, password, email, null, mustChangePassword: false);
        db.Add(teacher);
        await db.SaveChangesAsync();
        return new SeededAccount(teacher.Id, email, password, null);
    }

    /// <summary>Học sinh đã được GVCN cấp tài khoản (mật khẩu tạm, chưa kích hoạt).</summary>
    public async Task<SeededAccount> CreateStudentAccountAsync(SeededFund fund, string fullName = "Nguyễn Minh Khang", bool treasurer = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var username = Unique("hs.");
        var user = NewUser(scope, UserKind.Student, fullName, TemporaryPassword, null, username, mustChangePassword: true);
        var student = new Student(fund.Id, fullName, fund.OwnerUserId, now);
        student.LinkAccount(user.Id, now);
        db.AddRange(user, student);
        if (treasurer)
        {
            db.Add(new TreasurerAssignment(fund.Id, student.Id, fund.OwnerUserId, now));
        }

        await db.SaveChangesAsync();
        return new SeededAccount(user.Id, username, TemporaryPassword, student.Id);
    }

    /// <summary>Phụ huynh do GVCN tạo khi liên kết email với học sinh (mật khẩu tạm).</summary>
    public async Task<SeededAccount> CreateParentAccountAsync(SeededFund fund, params Guid[] studentIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var email = NewEmail();
        var parent = NewUser(scope, UserKind.Parent, "Phụ huynh", TemporaryPassword, email, null, mustChangePassword: true);
        db.Add(parent);
        foreach (var studentId in studentIds)
        {
            db.Add(new ParentLink(studentId, parent.Id, fund.OwnerUserId, now));
        }

        await db.SaveChangesAsync();
        return new SeededAccount(parent.Id, email, TemporaryPassword, null);
    }

    public async Task DisableUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.Disable(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task<User> FindUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
    }

    /// <summary>Giáo viên có hash tạo trước khi dùng pepper (PasswordHasher thuần).</summary>
    public async Task<SeededAccount> CreateTeacherWithLegacyHashAsync(string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = NewEmail();
        var legacyHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(null!, password);
        var teacher = new User(UserKind.Teacher, "Cô Hạnh", legacyHash, email, null, false, DateTimeOffset.UtcNow);
        db.Add(teacher);
        await db.SaveChangesAsync();
        return new SeededAccount(teacher.Id, email, password, null);
    }

    public async Task<List<AuditLog>> AuditLogsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = userId.ToString();
        return await db.AuditLogs.AsNoTracking()
            .Where(log => log.ActorUserId == userId || log.EntityId == id)
            .OrderBy(log => log.Id)
            .ToListAsync();
    }

    private static User NewUser(AsyncServiceScope scope, UserKind kind, string fullName, string password, string? email, string? username, bool mustChangePassword)
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        return new User(kind, fullName, hasher.Hash(password), email, username, mustChangePassword, DateTimeOffset.UtcNow);
    }
}

public sealed record SeededFund(Guid Id, string Name, Guid OwnerUserId);

public sealed record SeededAccount(Guid UserId, string Login, string Password, Guid? StudentId);
