using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Authorization;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;
using SchoolFund.Domain.Rules;

namespace SchoolFund.Application.Features.Students;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission(Student.Manage)]</c> kiểm tra ở API; service kiểm tra lại (roles-and-permissions.md mục 3.4).
/// Mật khẩu tạm chỉ nằm trong giá trị trả về, không bao giờ vào AuditLog (BR-37, NFR-10).
/// </remarks>
public sealed class StudentService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    AuditTrail auditTrail,
    TimeProvider timeProvider) : IStudentService
{
    public const string AccountExistsCode = "student.account_exists";
    public const string NoAccountCode = "student.no_account";
    public const string InvalidUsernameCode = "user.username";
    public const string UsernameTakenCode = "user.username_taken";
    public const string ParentLinkExistsCode = "parent_link.exists";
    public const string ParentEmailInUseCode = "parent.email_in_use";

    private const int SuggestAttempts = 20;

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<IReadOnlyList<StudentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        return await LoadAsync(fundId, studentId: null, cancellationToken);
    }

    public async Task<StudentResponse> GetAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        return (await LoadAsync(fundId, id, cancellationToken)).SingleOrDefault() ?? throw new NotFoundException();
    }

    public async Task<StudentCreatedResponse> CreateAsync(
        Guid fundId,
        string fullName,
        string? parentEmail,
        string? parentFullName,
        CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var student = new Student(fundId, fullName.Trim(), UserId, now);
        db.Add(student);
        auditTrail.Record(
            AuditActions.StudentCreate,
            AuditCategory.Change,
            UserId,
            nameof(Student),
            student.Id,
            newValues: new { fundId, fullName = student.FullName });

        // Học sinh mới chưa có liên kết; lỗi liên kết (email giáo viên…) làm hỏng cả thao tác – không lưu nửa vời.
        var parent = string.IsNullOrWhiteSpace(parentEmail)
            ? null
            : await AddParentLinkAsync(student, parentEmail, parentFullName, now, cancellationToken);

        await SaveAsync(cancellationToken);
        return new StudentCreatedResponse(student.Id, parent);
    }

    public async Task RenameAsync(Guid fundId, Guid id, string fullName, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        var student = await FindAsync(fundId, id, cancellationToken);

        var newName = fullName.Trim();
        if (student.FullName == newName)
        {
            return;
        }

        var oldName = student.FullName;
        student.Rename(newName, timeProvider.GetUtcNow());
        auditTrail.Record(
            AuditActions.StudentUpdate,
            AuditCategory.Change,
            UserId,
            nameof(Student),
            student.Id,
            oldValues: new { fullName = oldName },
            newValues: new { fullName = newName });
        await SaveAsync(cancellationToken);
    }

    public async Task<CredentialResponse> IssueAccountAsync(
        Guid fundId,
        Guid id,
        string? username,
        CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        var requested = string.IsNullOrWhiteSpace(username) ? null : LoginName.Normalize(username);
        if (requested is not null && !LoginName.IsValidUsername(requested))
        {
            throw new BusinessRuleException(
                InvalidUsernameCode,
                "Tên đăng nhập cần 3–60 ký tự: chữ thường không dấu, số, dấu chấm, gạch dưới, gạch ngang.");
        }

        // Khóa dòng học sinh: hai lệnh cấp đồng thời không tạo hai tài khoản cho cùng một học sinh.
        return await db.InTransactionAsync(
            async token =>
            {
                var student = await LockAsync(fundId, id, token);
                if (student.UserId is not null)
                {
                    throw new BusinessRuleException(AccountExistsCode, "Học sinh đã có tài khoản.");
                }

                string login;
                if (requested is not null)
                {
                    if (await UsernameTakenAsync(requested, token))
                    {
                        throw UsernameTaken();
                    }

                    login = requested;
                }
                else
                {
                    login = await SuggestFreeUsernameAsync(student.FullName, token);
                }

                var now = timeProvider.GetUtcNow();
                var temporaryPassword = Credentials.TemporaryPassword();
                var account = new User(UserKind.Student, student.FullName, passwordHasher.Hash(temporaryPassword), null, login, true, now);
                db.Add(account);
                student.LinkAccount(account.Id, now);
                auditTrail.Record(
                    AuditActions.StudentIssueAccount,
                    AuditCategory.Change,
                    UserId,
                    nameof(Student),
                    student.Id,
                    newValues: new { username = login, userId = account.Id });

                await SaveAsync(token);
                return new CredentialResponse(login, temporaryPassword);
            },
            cancellationToken);
    }

    public async Task<CredentialResponse> ResetPasswordAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        return await db.InTransactionAsync(
            async token =>
            {
                var student = await LockAsync(fundId, id, token);
                var account = student.UserId is { } accountId
                    ? await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == accountId), token)
                    : null;
                if (account is null)
                {
                    throw new BusinessRuleException(NoAccountCode, "Học sinh chưa có tài khoản.");
                }

                var temporaryPassword = Credentials.TemporaryPassword();
                account.ResetTemporaryPassword(passwordHasher.Hash(temporaryPassword), timeProvider.GetUtcNow());
                auditTrail.Record(
                    AuditActions.StudentResetPassword,
                    AuditCategory.Security,
                    UserId,
                    nameof(Student),
                    student.Id,
                    newValues: new { username = account.Username });

                await SaveAsync(token);
                return new CredentialResponse(account.Username!, temporaryPassword);
            },
            cancellationToken);
    }

    public async Task<ParentLinkResponse> LinkParentAsync(
        Guid fundId,
        Guid id,
        string email,
        string? fullName,
        CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        return await db.InTransactionAsync(
            async token =>
            {
                var student = await LockAsync(fundId, id, token);
                if (await db.AnyAsync(ActiveLinks(student.Id), token))
                {
                    throw ParentLinkExists();
                }

                var result = await AddParentLinkAsync(student, email, fullName, timeProvider.GetUtcNow(), token);
                await SaveAsync(token);
                return result;
            },
            cancellationToken);
    }

    public async Task UnlinkParentAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        await RequireManageAsync(fundId, cancellationToken);
        var student = await FindAsync(fundId, id, cancellationToken);
        var link = await db.FirstOrDefaultAsync(ActiveLinks(student.Id), cancellationToken) ?? throw new NotFoundException();

        link.Remove(UserId, timeProvider.GetUtcNow());
        auditTrail.Record(
            AuditActions.ParentLinkRemove,
            AuditCategory.Change,
            UserId,
            nameof(ParentLink),
            link.Id,
            oldValues: new { studentId = student.Id, parentUserId = link.ParentUserId, isActive = true },
            newValues: new { isActive = false });
        await SaveAsync(cancellationToken);
    }

    /// <summary>
    /// BR-30: email thuộc tài khoản phụ huynh → chỉ liên kết; chưa có → tạo tài khoản phụ huynh với mật khẩu tạm;
    /// email của giáo viên / học sinh → từ chối. Chỉ thêm vào change tracker, người gọi tự lưu.
    /// </summary>
    private async Task<ParentLinkResponse> AddParentLinkAsync(
        Student student,
        string email,
        string? fullName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = LoginName.Normalize(email);
        var existing = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(user => user.NormalizedEmail == normalizedEmail),
            cancellationToken);

        User parent;
        CredentialResponse? credential = null;
        if (existing is not null)
        {
            if (existing.Kind != UserKind.Parent)
            {
                throw new BusinessRuleException(
                    ParentEmailInUseCode,
                    "Email này đang dùng cho tài khoản giáo viên / học sinh, không liên kết làm phụ huynh được.");
            }

            parent = existing;
        }
        else
        {
            var temporaryPassword = Credentials.TemporaryPassword();
            var parentName = string.IsNullOrWhiteSpace(fullName) ? $"Phụ huynh {student.FullName}" : fullName.Trim();
            parent = new User(UserKind.Parent, parentName, passwordHasher.Hash(temporaryPassword), email.Trim(), null, true, now);
            db.Add(parent);
            credential = new CredentialResponse(parent.Email!, temporaryPassword);
            auditTrail.Record(
                AuditActions.ParentCreateAccount,
                AuditCategory.Change,
                UserId,
                nameof(User),
                parent.Id,
                newValues: new { email = parent.Email, fullName = parent.FullName });
        }

        var link = new ParentLink(student.Id, parent.Id, UserId, now);
        db.Add(link);
        auditTrail.Record(
            AuditActions.ParentLinkCreate,
            AuditCategory.Change,
            UserId,
            nameof(ParentLink),
            link.Id,
            newValues: new { studentId = student.Id, parentUserId = parent.Id, accountCreated = credential is not null });

        return new ParentLinkResponse(credential is not null, credential, EmailSent: false);
    }

    private async Task<IReadOnlyList<StudentResponse>> LoadAsync(Guid fundId, Guid? studentId, CancellationToken cancellationToken)
    {
        var studentsQuery = db.Query<Student>().Where(student => student.FundId == fundId);
        if (studentId is { } onlyId)
        {
            studentsQuery = studentsQuery.Where(student => student.Id == onlyId);
        }

        var students = await db.ToListAsync(studentsQuery, cancellationToken);
        if (students.Count == 0)
        {
            return [];
        }

        var studentIds = students.Select(student => student.Id).ToArray();
        var accountIds = students.Where(student => student.UserId is not null).Select(student => student.UserId!.Value).ToArray();

        var accounts = (await db.ToListAsync(
                db.Query<User>()
                    .Where(user => accountIds.Contains(user.Id))
                    .Select(user => new { user.Id, user.Username, user.Status, user.MustChangePassword }),
                cancellationToken))
            .ToDictionary(account => account.Id);

        var treasurerIds = (await db.ToListAsync(
                db.Query<TreasurerAssignment>()
                    .Where(assignment => assignment.FundId == fundId
                        && assignment.RevokedAt == null
                        && studentIds.Contains(assignment.StudentId))
                    .Select(assignment => assignment.StudentId),
                cancellationToken))
            .ToHashSet();

        var parents = (await db.ToListAsync(
                from link in db.Query<ParentLink>()
                where link.IsActive && studentIds.Contains(link.StudentId)
                join parent in db.Query<User>() on link.ParentUserId equals parent.Id
                select new { link.StudentId, parent.FullName, parent.Email, parent.MustChangePassword },
                cancellationToken))
            .ToDictionary(row => row.StudentId);

        return students
            .Select(student =>
            {
                var account = student.UserId is { } accountId ? accounts.GetValueOrDefault(accountId) : null;
                var status = account is null ? StudentAccountStatus.None
                    : account.Status == AccountStatus.Disabled ? StudentAccountStatus.Disabled
                    : account.MustChangePassword ? StudentAccountStatus.PendingActivation
                    : StudentAccountStatus.Active;
                var parent = parents.GetValueOrDefault(student.Id);
                return new StudentResponse(
                    student.Id,
                    student.FullName,
                    account?.Username,
                    status,
                    treasurerIds.Contains(student.Id),
                    parent is null ? null : new StudentParentResponse(parent.FullName, parent.Email, parent.MustChangePassword),
                    student.CreatedAt);
            })
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ThenBy(student => student.Id)
            .ToArray();
    }

    private async Task<string> SuggestFreeUsernameAsync(string fullName, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < SuggestAttempts; attempt++)
        {
            var candidate = Credentials.SuggestUsername(fullName);
            if (!await UsernameTakenAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ConflictException(UsernameTakenCode, "Không gợi ý được tên đăng nhập còn trống. Hãy nhập tên đăng nhập.");
    }

    private Task<bool> UsernameTakenAsync(string username, CancellationToken cancellationToken) =>
        db.AnyAsync(db.Query<User>().Where(user => user.Username == username), cancellationToken);

    private IQueryable<ParentLink> ActiveLinks(Guid studentId) =>
        db.Query<ParentLink>().Where(link => link.StudentId == studentId && link.IsActive);

    private async Task RequireManageAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.StudentManage))
        {
            throw new ForbiddenAccessException();
        }
    }

    private async Task<Student> FindAsync(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        await db.FirstOrDefaultAsync(
            db.Query<Student>().Where(student => student.Id == id && student.FundId == fundId),
            cancellationToken) ?? throw new NotFoundException();

    private async Task<Student> LockAsync(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        var student = await db.LockAsync<Student>(id, cancellationToken);
        return student is not null && student.FundId == fundId ? student : throw new NotFoundException();
    }

    /// <summary>Hai request cùng lúc đều qua được bước kiểm tra; ràng buộc UNIQUE là chốt chặn cuối.</summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
        {
            throw exception.ConstraintName switch
            {
                "UQ_User_Username" => UsernameTaken(),
                "UQ_Student_UserId" => new BusinessRuleException(AccountExistsCode, "Học sinh đã có tài khoản."),
                "UQ_ParentLink_Student_Active" => ParentLinkExists(),
                "UQ_User_NormalizedEmail" => new ConflictException(
                    "parent.email_conflict",
                    "Tài khoản phụ huynh với email này vừa được tạo bởi thao tác khác. Vui lòng thử lại."),
                _ => exception,
            };
        }
    }

    private static BusinessRuleException UsernameTaken() =>
        new(UsernameTakenCode, "Tên đăng nhập đã được sử dụng.");

    private static BusinessRuleException ParentLinkExists() =>
        new(ParentLinkExistsCode, "Học sinh đã có phụ huynh liên kết. Gỡ liên kết cũ trước khi liên kết email khác.");
}
