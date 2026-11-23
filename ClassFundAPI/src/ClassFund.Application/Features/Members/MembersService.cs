using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Members;

/// <inheritdoc />
public sealed class MembersService(
    IAppDb db,
    IPasswordHasher hasher,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail audit,
    TimeProvider clock,
    IEventInvitationService invitations) : IMembersService
{
    public async Task<PagedResult<MemberDto>> ListAsync(Guid fundId, MemberFilter filter, CancellationToken cancellationToken)
    {
        var status = filter.Status ?? MemberStatus.Active;
        var term = filter.Search?.Trim().ToLowerInvariant();
        var memberships = db.Query<FundMember>(); // dùng trong truy vấn con bên dưới (AZ-07)

        var query = from member in db.Query<FundMember>()
                    where member.FundId == fundId && member.Status == status
                    join user in db.Query<User>() on member.UserId equals user.Id
                    select new { member, user };

        if (filter.Role is { } role)
        {
            query = query.Where(row => row.member.Role == role);
        }

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(row => row.user.FullName.ToLower().Contains(term) || row.user.Email.Contains(term));
        }

        var total = await db.CountAsync(query, cancellationToken);
        var items = await db.ToListAsync(
            query
                .OrderBy(row => row.member.Role == MemberRole.Owner ? 0 : row.member.Role == MemberRole.Treasurer ? 1 : 2)
                .ThenBy(row => row.user.FullName)
                .ThenBy(row => row.member.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(row => new MemberDto(
                    row.member.Id,
                    row.user.Id,
                    row.user.FullName,
                    row.user.Email,
                    row.member.Role,
                    row.member.Status,
                    row.user.MustChangePassword,
                    row.member.JoinedAt,
                    row.member.RemovedAt,
                    row.user.Origin == UserOrigin.Provisioned
                        && row.user.MustChangePassword
                        && !memberships.Any(other => other.UserId == row.user.Id && other.FundId != fundId && other.Status == MemberStatus.Active))),
            cancellationToken);

        return new PagedResult<MemberDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<AddMemberResult> AddAsync(Guid fundId, string email, string? fullName, CancellationToken cancellationToken)
    {
        await EnsureFundActiveAsync(fundId, cancellationToken);
        var result = await AddOneAsync(fundId, email, fullName, cancellationToken);

        return result.Outcome switch
        {
            AddMemberOutcome.AlreadyExists => throw new ConflictException(MembersErrors.AlreadyExistsCode, MembersErrors.AlreadyExistsMessage),
            AddMemberOutcome.Invalid => throw new BusinessRuleException(MembersErrors.InvalidEmailCode, MembersErrors.InvalidEmailMessage),
            _ => result,
        };
    }

    public async Task<BulkAddMembersResult> AddManyAsync(Guid fundId, IReadOnlyList<BulkMemberInput> members, CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            throw new BusinessRuleException(MembersErrors.NoEmailsCode, MembersErrors.NoEmailsMessage);
        }

        if (members.Count > MembersErrors.MaxBulkEmails)
        {
            throw new BusinessRuleException(MembersErrors.TooManyEmailsCode, MembersErrors.TooManyEmailsMessage);
        }

        await EnsureFundActiveAsync(fundId, cancellationToken);

        // Chuẩn hóa và bỏ email trùng trong danh sách (giữ dòng xuất hiện đầu tiên).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = members
            .Select(item => new BulkMemberInput(item.Email.Trim(), string.IsNullOrWhiteSpace(item.FullName) ? null : item.FullName.Trim()))
            .Where(item => item.Email.Length > 0 && seen.Add(EmailAddress.Normalize(item.Email)))
            .ToList();
        if (unique.Count == 0)
        {
            throw new BusinessRuleException(MembersErrors.NoEmailsCode, MembersErrors.NoEmailsMessage);
        }

        var results = new List<AddMemberResult>(unique.Count);
        foreach (var item in unique)
        {
            if (item.FullName is { Length: > MembersErrors.FullNameMaxLength })
            {
                results.Add(new AddMemberResult(item.Email, AddMemberOutcome.Invalid, null, null, MembersErrors.InvalidNameMessage));
                continue;
            }

            results.Add(await AddOneAsync(fundId, item.Email, item.FullName, cancellationToken));
        }

        return new BulkAddMembersResult(results);
    }

    public async Task<MemberDto> UpdateAsync(Guid fundId, Guid memberId, string fullName, CancellationToken cancellationToken)
    {
        var name = NormalizeName(fullName);
        var (member, user) = await LoadManageableAsync(fundId, memberId, cancellationToken);
        await EnsureAccountManageableAsync(user, fundId, cancellationToken);

        var updated = await db.InTransactionAsync(
            async token =>
            {
                var lockedUser = await db.LockAsync<User>(user.Id, token) ?? throw new NotFoundException();
                var before = new { lockedUser.FullName };
                lockedUser.FullName = name;
                audit.Record(MemberAudit(MembersAuditActions.Update, fundId, member, before, new { lockedUser.FullName }));
                await db.SaveChangesAsync(token);
                return lockedUser;
            },
            cancellationToken);

        return ToDto(member, updated, canManageAccount: true);
    }

    public async Task<TemporaryPasswordResult> ResetPasswordAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        var (member, user) = await LoadManageableAsync(fundId, memberId, cancellationToken);
        await EnsureAccountManageableAsync(user, fundId, cancellationToken);
        var temporaryPassword = Credentials.TemporaryPassword();
        var passwordHash = hasher.Hash(temporaryPassword);

        await db.InTransactionAsync(
            async token =>
            {
                var lockedUser = await db.LockAsync<User>(user.Id, token) ?? throw new NotFoundException();
                lockedUser.PasswordHash = passwordHash;
                lockedUser.MustChangePassword = true;
                lockedUser.SecurityStamp = Guid.NewGuid().ToString("N"); // BR-29: phiên cũ mất hiệu lực
                audit.Record(MemberAudit(MembersAuditActions.ResetPassword, fundId, member, before: null, after: new { mustChangePassword = true }));
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return new TemporaryPasswordResult(temporaryPassword);
    }

    public async Task<RemovalPreviewDto> GetRemovalPreviewAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        var (member, _) = await LoadManageableAsync(fundId, memberId, cancellationToken);
        var impact = await MeasureAsync(fundId, member.UserId, cancellationToken);
        var blocked = BlockedReason(member, impact);

        return new RemovalPreviewDto(impact.Unpaid, impact.Balance, blocked is null, blocked);
    }

    public async Task<RemoveMemberResult> RemoveAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        // Kiểm tra quyền (AZ-02) trước khi khóa dòng; kiểm tra lại trạng thái trong transaction.
        await LoadManageableAsync(fundId, memberId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                var member = await db.LockAsync<FundMember>(memberId, token);
                if (member is null || member.FundId != fundId)
                {
                    throw new NotFoundException();
                }

                if (member.Status != MemberStatus.Active)
                {
                    throw new ConflictException(MembersErrors.AlreadyRemovedCode, MembersErrors.AlreadyRemovedMessage);
                }

                var impact = await MeasureAsync(fundId, member.UserId, token);
                if (BlockedReason(member, impact) is { } reason)
                {
                    throw new BusinessRuleException(CodeFor(member, impact), reason);
                }

                // OQ-15: nghĩa vụ chưa đóng của khoản thu đang mở bị hủy; dữ liệu lịch sử được giữ nguyên (BR-30).
                var open = await db.ToListAsync(UnpaidOpenAssignments(fundId, member.UserId), token);
                foreach (var assignment in open)
                {
                    assignment.Status = AssignmentStatus.Cancelled;
                }

                member.Status = MemberStatus.Removed;
                member.RemovedAt = clock.GetUtcNow();
                audit.Record(MemberAudit(
                    MembersAuditActions.Remove,
                    fundId,
                    member,
                    before: new { status = nameof(MemberStatus.Active) },
                    after: new { status = nameof(MemberStatus.Removed), cancelledAssignments = open.Count }));
                await db.SaveChangesAsync(token);
                return new RemoveMemberResult(open.Count);
            },
            cancellationToken);
    }

    // ---------- Thêm một email ----------

    private async Task<AddMemberResult> AddOneAsync(Guid fundId, string rawEmail, string? fullName, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(rawEmail);
        if (!EmailAddress.IsValid(email))
        {
            return new AddMemberResult(rawEmail.Trim(), AddMemberOutcome.Invalid, null, null, MembersErrors.InvalidEmailMessage);
        }

        // null = thêm hàng loạt (chỉ có email): lấy phần trước @ làm tên tạm, thủ quỹ sửa sau; có truyền tên thì phải hợp lệ.
        var name = fullName is null ? email[..email.IndexOf('@')] : NormalizeName(fullName);
        var actorId = RequireActorId();

        // Chỉ tốn công băm khi thật sự tạo tài khoản mới.
        string? temporaryPassword = null;
        string? passwordHash = null;
        if (!await db.AnyAsync(db.Query<User>().Where(item => item.Email == email), cancellationToken))
        {
            temporaryPassword = Credentials.TemporaryPassword();
            passwordHash = hasher.Hash(temporaryPassword);
        }

        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    var now = clock.GetUtcNow();
                    var user = await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Email == email), token);
                    FundMember member;
                    AddMemberOutcome outcome;
                    string? issuedPassword = null;
                    string action = MembersAuditActions.Add;

                    if (user is null)
                    {
                        // Có thể có người vừa tạo tài khoản này giữa hai lần đọc: khi đó chưa có mật khẩu băm sẵn.
                        user = new User(email, name, passwordHash ?? hasher.Hash(Credentials.TemporaryPassword()), UserOrigin.Provisioned, mustChangePassword: true, now);
                        db.Add(user);
                        member = new FundMember(fundId, user.Id, MemberRole.Student, now);
                        db.Add(member);
                        outcome = AddMemberOutcome.Created;
                        issuedPassword = temporaryPassword;
                    }
                    else
                    {
                        var existing = await db.FirstOrDefaultAsync(
                            db.Query<FundMember>().Where(item => item.FundId == fundId && item.UserId == user.Id), token);
                        if (existing is null)
                        {
                            member = new FundMember(fundId, user.Id, MemberRole.Student, now);
                            db.Add(member);
                            outcome = AddMemberOutcome.ExistingAdded;
                        }
                        else if (existing.Status == MemberStatus.Active)
                        {
                            return new AddMemberResult(email, AddMemberOutcome.AlreadyExists, await ToDtoAsync(existing, user, fundId, token), null, MembersErrors.AlreadyExistsMessage);
                        }
                        else
                        {
                            // Kích hoạt lại luôn về học sinh: quyền thủ quỹ không tự quay lại sau khi bị ngừng.
                            existing.Status = MemberStatus.Active;
                            existing.RemovedAt = null;
                            existing.Role = MemberRole.Student;
                            member = existing;
                            outcome = AddMemberOutcome.Reactivated;
                            action = MembersAuditActions.Reactivate;
                        }
                    }

                    var charges = await AssignOpenChargesAsync(fundId, user.Id, token);

                    // BR-17 / TD-10: thành viên mới hoặc quay lại được mời vào các sự kiện đang mở.
                    await invitations.InviteToOpenEventsAsync(fundId, user.Id, actorId, token);

                    audit.Record(new AuditEntry(
                        action,
                        fundId,
                        actorId,
                        FinancialScope.General,
                        EntityType: nameof(FundMember),
                        EntityId: member.Id,
                        After: new { userId = user.Id, user.Email, role = member.Role.ToString(), outcome = outcome.ToString(), assignedCharges = charges }));
                    await db.SaveChangesAsync(token);
                    return new AddMemberResult(email, outcome, await ToDtoAsync(member, user, fundId, token), issuedPassword, null);
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Hai request cùng email / cùng thành viên đua nhau: ràng buộc UNIQUE là chốt chặn cuối.
            return new AddMemberResult(email, AddMemberOutcome.AlreadyExists, null, null, MembersErrors.AlreadyExistsMessage);
        }
    }

    /// <summary>
    /// Thành viên mới (hoặc được thêm lại) vẫn phải đóng các khoản thu chung đang mở: tạo nghĩa vụ còn thiếu
    /// và khôi phục nghĩa vụ đã bị hủy khi ngừng (quyết toán sau đó chia theo tỷ lệ số đã đóng nên công bằng).
    /// </summary>
    private async Task<int> AssignOpenChargesAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var charges = await db.ToListAsync(
            db.Query<Charge>().Where(charge => charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open),
            cancellationToken);
        if (charges.Count == 0)
        {
            return 0;
        }

        var chargeIds = charges.Select(charge => charge.Id).ToArray();
        var existing = (await db.ToListAsync(
                db.Query<ChargeAssignment>().Where(item => item.UserId == userId && chargeIds.Contains(item.ChargeId)),
                cancellationToken))
            .ToDictionary(item => item.ChargeId);

        var count = 0;
        foreach (var charge in charges)
        {
            if (!existing.TryGetValue(charge.Id, out var assignment))
            {
                db.Add(new ChargeAssignment(charge.Id, userId, charge.AmountPerPerson));
                count++;
            }
            else if (assignment.Status == AssignmentStatus.Cancelled)
            {
                assignment.Status = AssignmentStatus.Unpaid;
                count++;
            }
        }

        return count;
    }

    // ---------- Quyền theo dữ liệu ----------

    /// <summary>Tìm thành viên của quỹ và áp AZ-02 (thủ quỹ không tác động lên thủ quỹ chính / thủ quỹ khác).</summary>
    private async Task<(FundMember Member, User User)> LoadManageableAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        var member = await db.FirstOrDefaultAsync(
            db.Query<FundMember>().Where(item => item.Id == memberId && item.FundId == fundId), cancellationToken)
            ?? throw new NotFoundException();

        var access = await accessService.GetFundAccessAsync(actorId, fundId, cancellationToken);
        if (member.Role != MemberRole.Student && access.Role != MemberRole.Owner)
        {
            throw new ForbiddenAccessException(message: MembersErrors.ProtectedRoleMessage); // AZ-02
        }

        var user = await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Id == member.UserId), cancellationToken)
            ?? throw new NotFoundException();
        return (member, user);
    }

    /// <summary>
    /// AZ-07: chỉ sửa họ tên / cấp lại mật khẩu cho tài khoản do thủ quỹ cấp (Provisioned) và chỉ thuộc quỹ này.
    /// Nếu không, một thủ quỹ có thể chiếm tài khoản của người dùng tự đăng ký hoặc đang ở quỹ khác.
    /// </summary>
    private async Task EnsureAccountManageableAsync(User user, Guid fundId, CancellationToken cancellationToken)
    {
        if (user.Id == RequireActorId())
        {
            throw new BusinessRuleException(MembersErrors.CannotActOnSelfCode, MembersErrors.CannotActOnSelfMessage);
        }

        if (await IsManagedAsync(user, fundId, cancellationToken))
        {
            return;
        }

        // Tài khoản do thủ quỹ cấp nhưng học sinh đã tự đặt mật khẩu: báo rõ lý do thay vì thông báo chung.
        var ownedByStudent = user.Origin == UserOrigin.Provisioned && !user.MustChangePassword
            && !await db.AnyAsync(
                db.Query<FundMember>().Where(item => item.UserId == user.Id && item.FundId != fundId && item.Status == MemberStatus.Active),
                cancellationToken);
        throw ownedByStudent
            ? new BusinessRuleException(MembersErrors.PasswordAlreadySetCode, MembersErrors.PasswordAlreadySetMessage)
            : new BusinessRuleException(MembersErrors.AccountNotManagedCode, MembersErrors.AccountNotManagedMessage);
    }

    // ---------- Ngừng thành viên ----------

    private sealed record Impact(int Unpaid, long Balance);

    private IQueryable<ChargeAssignment> UnpaidOpenAssignments(Guid fundId, Guid userId) =>
        from assignment in db.Query<ChargeAssignment>()
        where assignment.UserId == userId && assignment.Status == AssignmentStatus.Unpaid
        join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
        where charge.FundId == fundId && charge.Status == ChargeStatus.Open
        select assignment;

    private async Task<Impact> MeasureAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var unpaid = await db.CountAsync(UnpaidOpenAssignments(fundId, userId), cancellationToken);
        var balance = (await db.ToListAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId && item.UserId == userId).Select(item => item.Balance),
            cancellationToken)).Sum();

        return new Impact(unpaid, balance);
    }

    private static string? BlockedReason(FundMember member, Impact impact) =>
        member.Role == MemberRole.Owner ? MembersErrors.CannotRemoveOwnerMessage
        : member.Status != MemberStatus.Active ? MembersErrors.AlreadyRemovedMessage
        : impact.Balance > 0 ? MembersErrors.HasBalanceMessage
        : null;

    private static string CodeFor(FundMember member, Impact impact) =>
        member.Role == MemberRole.Owner ? MembersErrors.CannotRemoveOwnerCode
        : MembersErrors.HasBalanceCode;

    // ---------- Chung ----------

    private async Task EnsureFundActiveAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var status = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(item => item.Id == fundId).Select(item => (FundStatus?)item.Status), cancellationToken)
            ?? throw new NotFoundException();
        YearEndRule.EnsureFundActive(status);
    }

    private AuditEntry MemberAudit(string action, Guid fundId, FundMember member, object? before, object? after) =>
        new(action, fundId, RequireActorId(), FinancialScope.General, EntityType: nameof(FundMember), EntityId: member.Id, Before: before, After: after);

    private Guid RequireActorId() =>
        currentUser.UserId
        ?? throw new AuthenticationFailedException(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");

    private static MemberDto ToDto(FundMember member, User user, bool canManageAccount) =>
        new(member.Id, user.Id, user.FullName, user.Email, member.Role, member.Status, user.MustChangePassword, member.JoinedAt, member.RemovedAt, canManageAccount);

    private async Task<MemberDto> ToDtoAsync(FundMember member, User user, Guid fundId, CancellationToken cancellationToken) =>
        ToDto(member, user, await IsManagedAsync(user, fundId, cancellationToken));

    /// <summary>
    /// AZ-07: thủ quỹ chỉ quản lý (sửa tên, cấp mật khẩu tạm) tài khoản do mình cấp, CHƯA được học sinh tự đặt mật khẩu
    /// (<c>MustChangePassword</c> còn true) và không thuộc quỹ hoạt động nào khác.
    /// </summary>
    private async Task<bool> IsManagedAsync(User user, Guid fundId, CancellationToken cancellationToken) =>
        user.Origin == UserOrigin.Provisioned
        && user.MustChangePassword
        && !await db.AnyAsync(
            db.Query<FundMember>().Where(item => item.UserId == user.Id && item.FundId != fundId && item.Status == MemberStatus.Active),
            cancellationToken);

    private static string NormalizeName(string fullName)
    {
        var name = fullName.Trim();
        return name.Length is 0 or > MembersErrors.FullNameMaxLength
            ? throw new BusinessRuleException(MembersErrors.InvalidNameCode, MembersErrors.InvalidNameMessage)
            : name;
    }
}
