using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Auth;

/// <summary>
/// M1 – Auth & Tài khoản (FR-20, FR-13, FR-21; BR-17, BR-18, BR-26, BR-28).
/// Không ghi mật khẩu, hash, cookie vào nhật ký (NFR-10).
/// </summary>
public sealed class AuthService(
    IAppDb db,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail auditTrail,
    TimeProvider clock) : IAuthService
{
    private const string EmailUniqueConstraint = "UQ_User_NormalizedEmail";

    /// <summary>Hash giả: đăng nhập sai tên tốn thời gian như sai mật khẩu (không lộ tài khoản có tồn tại).</summary>
    private static string? dummyPasswordHash;

    public async Task<AuthSession> RegisterTeacherAsync(RegisterTeacherCommand command, CancellationToken cancellationToken)
    {
        RequirePasswordPolicy(command.Password);

        var normalizedEmail = LoginName.Normalize(command.Email);
        if (await db.AnyAsync(db.Query<User>().Where(user => user.NormalizedEmail == normalizedEmail), cancellationToken))
        {
            throw AuthErrors.EmailTaken();
        }

        var now = clock.GetUtcNow();
        var teacher = new User(
            UserKind.Teacher,
            command.FullName.Trim(),
            passwordHasher.Hash(command.Password),
            command.Email.Trim(),
            username: null,
            mustChangePassword: false,
            now);
        teacher.RecordSignIn(now);
        db.Add(teacher);
        auditTrail.Record(
            AuthAuditActions.RegisterTeacher,
            AuditCategory.Change,
            teacher.Id,
            nameof(User),
            teacher.Id,
            newValues: new { teacher.FullName, teacher.Email, Kind = teacher.Kind.ToString() });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == EmailUniqueConstraint)
        {
            // Hai request đăng ký cùng email đồng thời.
            throw AuthErrors.EmailTaken();
        }

        return await SignedInAsync(teacher, cancellationToken);
    }

    public async Task<AuthSession> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var key = LoginName.Normalize(login);
        var user = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(candidate => candidate.NormalizedEmail == key || candidate.Username == key),
            cancellationToken);

        var check = user is null ? VerifyAgainstDummy(password) : passwordHasher.Verify(user.PasswordHash, password);

        if (user is null || check == PasswordCheck.Failed)
        {
            await RecordSignInFailedAsync(user, "invalid_credentials", cancellationToken);
            throw AuthErrors.InvalidCredentials();
        }

        // Chỉ báo "bị khóa" khi mật khẩu đúng (BR-26).
        if (user.Status != AccountStatus.Active)
        {
            await RecordSignInFailedAsync(user, "account_disabled", cancellationToken);
            throw AuthErrors.AccountDisabled();
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(passwordHasher.Hash(password));
        }

        user.RecordSignIn(clock.GetUtcNow());
        auditTrail.Record(AuthAuditActions.SignIn, AuditCategory.Security, user.Id, nameof(User), user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return await SignedInAsync(user, cancellationToken);
    }

    public async Task<MeDto> GetMeAsync(CancellationToken cancellationToken) =>
        await ToMeAsync(await LoadCurrentUserAsync(cancellationToken), cancellationToken);

    public async Task<MeDto> UpdateMeAsync(string fullName, CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        var oldName = user.FullName;
        var newName = fullName.Trim();

        if (newName != oldName)
        {
            user.Rename(newName, clock.GetUtcNow());
            auditTrail.Record(
                AuthAuditActions.UpdateProfile,
                AuditCategory.Change,
                user.Id,
                nameof(User),
                user.Id,
                oldValues: new { FullName = oldName },
                newValues: new { user.FullName });
            await db.SaveChangesAsync(cancellationToken);
        }

        return await ToMeAsync(user, cancellationToken);
    }

    public async Task<AuthSession> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);

        if (passwordHasher.Verify(user.PasswordHash, command.CurrentPassword) == PasswordCheck.Failed)
        {
            throw AuthErrors.WrongPassword();
        }

        RequirePasswordPolicy(command.NewPassword);

        if (passwordHasher.Verify(user.PasswordHash, command.NewPassword) != PasswordCheck.Failed)
        {
            throw AuthErrors.PasswordReused();
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock.GetUtcNow());
        auditTrail.Record(AuthAuditActions.PasswordChanged, AuditCategory.Security, user.Id, nameof(User), user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return await SignedInAsync(user, cancellationToken);
    }

    private async Task<AuthSession> SignedInAsync(User user, CancellationToken cancellationToken) =>
        new(new SessionTicket(user.Id, user.SecurityStamp), await ToMeAsync(user, cancellationToken));

    private async Task<MeDto> ToMeAsync(User user, CancellationToken cancellationToken)
    {
        var memberships = await accessService.GetMembershipsAsync(user.Id, cancellationToken);
        return new MeDto(
            user.Id,
            user.Kind,
            user.FullName,
            user.Email,
            user.Username,
            user.MustChangePassword,
            Sorted(RolePermissions.ForAccount(user.Kind)),
            memberships
                .Select(membership => new MembershipDto(
                    membership.FundId,
                    membership.FundName,
                    membership.Role,
                    Sorted(membership.Permissions),
                    membership.LinkedStudents))
                .ToArray());
    }

    private async Task<User> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw AuthErrors.NotSignedIn();
        return await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == userId), cancellationToken)
            ?? throw AuthErrors.NotSignedIn();
    }

    private async Task RecordSignInFailedAsync(User? user, string reason, CancellationToken cancellationToken)
    {
        // Không lưu chuỗi đăng nhập hay mật khẩu đã nhập: có thể là mật khẩu gõ nhầm ô.
        auditTrail.Record(
            AuthAuditActions.SignInFailed,
            AuditCategory.Security,
            actorUserId: null,
            user is null ? null : nameof(User),
            user?.Id,
            newValues: new { Reason = reason });
        await db.SaveChangesAsync(cancellationToken);
    }

    private PasswordCheck VerifyAgainstDummy(string password)
    {
        dummyPasswordHash ??= passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        passwordHasher.Verify(dummyPasswordHash, password);
        return PasswordCheck.Failed;
    }

    private static void RequirePasswordPolicy(string password)
    {
        if (!PasswordPolicy.IsSatisfiedBy(password))
        {
            throw AuthErrors.PasswordPolicyViolated();
        }
    }

    private static string[] Sorted(IEnumerable<string> permissions) => permissions.Order(StringComparer.Ordinal).ToArray();
}
