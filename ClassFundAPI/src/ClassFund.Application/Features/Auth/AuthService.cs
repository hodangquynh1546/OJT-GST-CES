using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Auth;

/// <inheritdoc />
public sealed class AuthService(
    IAppDb db,
    IPasswordHasher hasher,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail audit,
    TimeProvider clock) : IAuthService
{
    // Hash giả để đăng nhập email không tồn tại cũng tốn thời gian băm như email có thật (chống dò email qua thời gian phản hồi).
    private readonly Lazy<string> _dummyHash = new(() => hasher.Hash(Credentials.TemporaryPassword()));

    public async Task<AuthSession> RegisterAsync(string fullName, string email, string password, CancellationToken cancellationToken)
    {
        var name = NormalizeName(fullName);
        var normalizedEmail = EmailAddress.Normalize(email);
        if (!EmailAddress.IsValid(normalizedEmail))
        {
            throw new BusinessRuleException(AuthErrors.InvalidEmailCode, AuthErrors.InvalidEmailMessage);
        }

        EnsurePasswordPolicy(password);
        var passwordHash = hasher.Hash(password);

        User user;
        try
        {
            user = await db.InTransactionAsync(
                async token =>
                {
                    if (await db.AnyAsync(db.Query<User>().Where(item => item.Email == normalizedEmail), token))
                    {
                        throw new ConflictException(AuthErrors.EmailExistsCode, AuthErrors.EmailExistsMessage);
                    }

                    var created = new User(normalizedEmail, name, passwordHash, UserOrigin.SelfRegistered, false, clock.GetUtcNow());
                    db.Add(created);
                    audit.Record(new AuditEntry(
                        AuthAuditActions.Register,
                        FundId: null,
                        created.Id,
                        FinancialScope.System,
                        EntityType: nameof(User),
                        EntityId: created.Id,
                        After: new { created.Email, created.FullName }));
                    await db.SaveChangesAsync(token);
                    return created;
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Hai request cùng email đua nhau: ràng buộc UX_User_Email là chốt chặn cuối.
            throw new ConflictException(AuthErrors.EmailExistsCode, AuthErrors.EmailExistsMessage);
        }

        return await BuildSessionAsync(user, cancellationToken);
    }

    public async Task<AuthSession> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailAddress.Normalize(email);
        var user = await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Email == normalizedEmail), cancellationToken);

        if (user is null)
        {
            hasher.Verify(_dummyHash.Value, password);
            throw new AuthenticationFailedException(AuthenticationFailedException.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage);
        }

        var check = hasher.Verify(user.PasswordHash, password);
        if (check == PasswordCheck.Failed)
        {
            throw new AuthenticationFailedException(AuthenticationFailedException.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage);
        }

        // Báo khóa tài khoản chỉ sau khi mật khẩu đúng, để người ngoài không dò được tài khoản nào đang bị khóa.
        if (!user.IsActive)
        {
            throw new AuthenticationFailedException(AuthenticationFailedException.AccountDisabledCode, AuthErrors.AccountDisabledMessage);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            await RehashAsync(user.Id, password, cancellationToken);
            user = await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Id == user.Id), cancellationToken) ?? user;
        }

        return await BuildSessionAsync(user, cancellationToken);
    }

    public async Task<CurrentUserDto> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(string fullName, CancellationToken cancellationToken)
    {
        var name = NormalizeName(fullName);
        var userId = RequireUserId();

        var user = await db.InTransactionAsync(
            async token =>
            {
                var locked = await db.LockAsync<User>(userId, token) ?? throw new NotFoundException();
                var before = new { locked.FullName };
                locked.FullName = name;
                audit.Record(new AuditEntry(
                    AuthAuditActions.UpdateProfile,
                    FundId: null,
                    userId,
                    FinancialScope.System,
                    EntityType: nameof(User),
                    EntityId: userId,
                    Before: before,
                    After: new { locked.FullName }));
                await db.SaveChangesAsync(token);
                return locked;
            },
            cancellationToken);

        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<AuthSession> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        EnsurePasswordPolicy(newPassword);

        var user = await db.InTransactionAsync(
            async token =>
            {
                var locked = await db.LockAsync<User>(userId, token) ?? throw new NotFoundException();

                if (hasher.Verify(locked.PasswordHash, currentPassword) == PasswordCheck.Failed)
                {
                    // 422, không phải 401: client hiểu 401 là hết phiên và đá về trang đăng nhập.
                    throw new BusinessRuleException(AuthErrors.CurrentPasswordIncorrectCode, AuthErrors.CurrentPasswordIncorrectMessage);
                }

                if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
                {
                    throw new BusinessRuleException(AuthErrors.PasswordUnchangedCode, AuthErrors.PasswordUnchangedMessage);
                }

                var wasTemporary = locked.MustChangePassword;
                locked.PasswordHash = hasher.Hash(newPassword);
                locked.MustChangePassword = false;
                locked.SecurityStamp = Guid.NewGuid().ToString("N");
                audit.Record(new AuditEntry(
                    AuthAuditActions.ChangePassword,
                    FundId: null,
                    userId,
                    FinancialScope.System,
                    EntityType: nameof(User),
                    EntityId: userId,
                    Before: new { mustChangePassword = wasTemporary },
                    After: new { mustChangePassword = false },
                    IsSelfAction: true));
                await db.SaveChangesAsync(token);
                return locked;
            },
            cancellationToken);

        return await BuildSessionAsync(user, cancellationToken);
    }

    private async Task RehashAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        // Thay đổi kỹ thuật (cùng mật khẩu, tham số băm mới), không phải thao tác nghiệp vụ nên không ghi audit.
        await db.InTransactionAsync(
            async token =>
            {
                var locked = await db.LockAsync<User>(userId, token) ?? throw new NotFoundException();
                locked.PasswordHash = hasher.Hash(password);
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);
    }

    private async Task<AuthSession> BuildSessionAsync(User user, CancellationToken cancellationToken) =>
        new(new SessionTicket(user.Id, user.SecurityStamp), await ToDtoAsync(user, cancellationToken));

    private async Task<User> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        return await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Id == userId && item.IsActive), cancellationToken)
            ?? throw new AuthenticationFailedException(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");
    }

    private async Task<CurrentUserDto> ToDtoAsync(User user, CancellationToken cancellationToken)
    {
        var accountPermissions = await accessService.GetAccountPermissionsAsync(user.Id, cancellationToken);
        var memberships = await accessService.GetMembershipsAsync(user.Id, cancellationToken);

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.FullName,
            user.MustChangePassword,
            accountPermissions.Order(StringComparer.Ordinal).ToArray(),
            memberships
                .Select(item => new MembershipDto(item.FundId, item.FundName, item.Role, item.Permissions.Order(StringComparer.Ordinal).ToArray()))
                .ToArray());
    }

    private Guid RequireUserId() =>
        currentUser.UserId
        ?? throw new AuthenticationFailedException(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");

    private static string NormalizeName(string fullName)
    {
        var name = fullName.Trim();
        return name.Length is 0 or > AuthErrors.FullNameMaxLength
            ? throw new BusinessRuleException(AuthErrors.InvalidNameCode, AuthErrors.InvalidNameMessage)
            : name;
    }

    private static void EnsurePasswordPolicy(string password)
    {
        if (!PasswordPolicy.IsSatisfiedBy(password))
        {
            throw new BusinessRuleException(PasswordPolicy.ViolationCode, PasswordPolicy.ViolationMessage);
        }
    }
}
