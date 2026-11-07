using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Auth;

/// <summary>
/// Đăng ký giáo viên, đăng nhập một ô (không chọn vai trò), tài khoản cá nhân, đổi mật khẩu
/// (FR-20, FR-13, FR-21; BR-17, BR-26, BR-28). Không ghi mật khẩu / hash vào nhật ký (NFR-10).
/// </summary>
public sealed class AuthService(
    IAppDb db,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail auditTrail,
    TimeProvider clock)
{
    public const string EmailTakenCode = "user.email_taken";
    public const string WrongPasswordCode = "auth.wrong_password";
    public const string PasswordReusedCode = "auth.password_reused";
    private const string EmailUniqueConstraint = "UQ_User_NormalizedEmail";

    /// <summary>Hash giả để đăng nhập sai tên tốn thời gian như sai mật khẩu (không lộ tài khoản tồn tại).</summary>
    private static string? dummyPasswordHash;

    public async Task<AuthSessionResult> RegisterTeacherAsync(RegisterTeacherCommand command, CancellationToken cancellationToken)
    {
        RequirePasswordPolicy(command.Password);

        var normalizedEmail = LoginName.Normalize(command.Email);
        if (await db.AnyAsync(db.Query<User>().Where(user => user.NormalizedEmail == normalizedEmail), cancellationToken))
        {
            throw EmailTaken();
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
            AuditActions.AccountRegisterTeacher,
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
            throw EmailTaken();
        }

        return await SignedInAsync(teacher, cancellationToken);
    }

    /// <summary><paramref name="login"/> là email (giáo viên, phụ huynh) hoặc tên đăng nhập (học sinh); vai trò do hệ thống xác định (D-34).</summary>
    public async Task<AuthSessionResult> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var key = LoginName.Normalize(login);
        var user = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(candidate => candidate.NormalizedEmail == key || candidate.Username == key),
            cancellationToken);

        var check = user is null
            ? VerifyAgainstDummy(password)
            : passwordHasher.Verify(user.PasswordHash, password);

        if (user is null || check == PasswordCheck.Failed)
        {
            await RecordSignInFailedAsync(user, "invalid_credentials", cancellationToken);
            throw new AuthenticationFailedException(
                AuthenticationFailedException.InvalidCredentialsCode,
                "Email / tên đăng nhập hoặc mật khẩu không đúng.");
        }

        // Chỉ báo "bị khóa" khi mật khẩu đúng (BR-26).
        if (user.Status != AccountStatus.Active)
        {
            await RecordSignInFailedAsync(user, "account_disabled", cancellationToken);
            throw new AuthenticationFailedException(
                AuthenticationFailedException.AccountDisabledCode,
                "Tài khoản đang bị khóa, liên hệ giáo viên chủ nhiệm.");
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(passwordHasher.Hash(password));
        }

        user.RecordSignIn(clock.GetUtcNow());
        auditTrail.Record(AuditActions.AuthSignIn, AuditCategory.Security, user.Id, nameof(User), user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return await SignedInAsync(user, cancellationToken);
    }

    public async Task<MeResult> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        return await ToMeAsync(user, cancellationToken);
    }

    public async Task<MeResult> UpdateMeAsync(string fullName, CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        var oldName = user.FullName;
        var newName = fullName.Trim();

        if (newName != oldName)
        {
            user.Rename(newName, clock.GetUtcNow());
            auditTrail.Record(
                AuditActions.AccountUpdateProfile,
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

    /// <summary>Đổi mật khẩu: xóa cờ mật khẩu tạm, SecurityStamp mới → mọi phiên khác hết hiệu lực; trả phiên mới cho thiết bị hiện tại.</summary>
    public async Task<AuthSessionResult> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);

        if (passwordHasher.Verify(user.PasswordHash, command.CurrentPassword) == PasswordCheck.Failed)
        {
            throw new BusinessRuleException(WrongPasswordCode, "Mật khẩu hiện tại không đúng.");
        }

        RequirePasswordPolicy(command.NewPassword);

        if (passwordHasher.Verify(user.PasswordHash, command.NewPassword) != PasswordCheck.Failed)
        {
            throw new BusinessRuleException(PasswordReusedCode, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock.GetUtcNow());
        auditTrail.Record(AuditActions.AuthPasswordChanged, AuditCategory.Security, user.Id, nameof(User), user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return await SignedInAsync(user, cancellationToken);
    }

    private async Task<AuthSessionResult> SignedInAsync(User user, CancellationToken cancellationToken) =>
        new(new SessionTicket(user.Id, user.SecurityStamp), await ToMeAsync(user, cancellationToken));

    private async Task<MeResult> ToMeAsync(User user, CancellationToken cancellationToken) =>
        new(
            user.Id,
            user.Kind,
            user.FullName,
            user.Email,
            user.Username,
            user.MustChangePassword,
            RolePermissions.ForAccount(user.Kind),
            await accessService.GetMembershipsAsync(user.Id, cancellationToken));

    private async Task<User> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw NotSignedIn();
        return await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == userId), cancellationToken)
            ?? throw NotSignedIn();
    }

    private async Task RecordSignInFailedAsync(User? user, string reason, CancellationToken cancellationToken)
    {
        // Không lưu chuỗi đăng nhập hay mật khẩu đã nhập: có thể là mật khẩu gõ nhầm ô.
        auditTrail.Record(
            AuditActions.AuthSignInFailed,
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
            throw new BusinessRuleException(PasswordPolicy.ViolationCode, PasswordPolicy.ViolationMessage);
        }
    }

    private static BusinessRuleException EmailTaken() =>
        new(EmailTakenCode, "Email này đã được dùng cho một tài khoản khác.");

    private static AuthenticationFailedException NotSignedIn() =>
        new(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");
}
