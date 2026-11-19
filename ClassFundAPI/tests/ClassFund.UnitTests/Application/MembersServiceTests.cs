using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Members;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using Moq;

namespace ClassFund.UnitTests.Application;

/// <summary>M3-T3: MembersService (FR-09 → FR-11, AZ-02, AZ-07, BR-30, OQ-15, OQ-20).</summary>
public class MembersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _owner;
    private readonly User _treasurer;
    private readonly Fund _fund;
    private readonly FundMember _ownerMember;
    private readonly FundMember _treasurerMember;

    public MembersServiceTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(password => "h:" + password);

        _owner = new User("owner@x.com", "Thủ quỹ chính", "h", UserOrigin.SelfRegistered, false, Now);
        _treasurer = new User("treasurer@x.com", "Thủ quỹ", "h", UserOrigin.Provisioned, false, Now);
        _fund = new Fund("Lớp 10A1", _owner.Id, "2026-2027", Now);
        _ownerMember = new FundMember(_fund.Id, _owner.Id, MemberRole.Owner, Now);
        _treasurerMember = new FundMember(_fund.Id, _treasurer.Id, MemberRole.Treasurer, Now);
        _db.Seed(_owner, _treasurer);
        _db.Seed(_fund);
        _db.Seed(_ownerMember, _treasurerMember);
        ActAs(_owner);
    }

    private void ActAs(User user) => _currentUser.SetupGet(c => c.UserId).Returns(user.Id);

    private MembersService CreateService()
    {
        var request = new Mock<IRequestInfo>();
        return new MembersService(_db, _hasher.Object, new AccessService(_db), _currentUser.Object, new AuditTrail(_db, request.Object, new FixedClock()), new FixedClock());
    }

    private (User User, FundMember Member) SeedStudent(string email = "hs@x.com", string name = "Học Sinh", UserOrigin origin = UserOrigin.Provisioned, MemberStatus status = MemberStatus.Active, Fund? fund = null, bool passwordSet = false)
    {
        // Học sinh do thủ quỹ cấp: chưa đăng nhập lần đầu nên còn phải đổi mật khẩu tạm (passwordSet = đã tự đặt mật khẩu).
        var user = new User(email, name, "h:old", origin, mustChangePassword: !passwordSet, Now);
        var member = new FundMember((fund ?? _fund).Id, user.Id, MemberRole.Student, Now) { Status = status };
        _db.Seed(user);
        _db.Seed(member);
        return (user, member);
    }

    private static BulkMemberInput[] Rows(params string[] lines) =>
        lines.Select(line => line.Split('|') is [var email, var name] ? new BulkMemberInput(email, name) : new BulkMemberInput(line, null)).ToArray();

    private static MemberFilter Filter(string? search = null, MemberRole? role = null, MemberStatus? status = null, int page = 1, int pageSize = 20) =>
        new(search, role, status, page, pageSize);

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_defaults_to_active_members_with_owner_then_treasurer_then_students()
    {
        SeedStudent("b@x.com", "Bình");
        SeedStudent("a@x.com", "An");
        SeedStudent("gone@x.com", "Đã nghỉ", status: MemberStatus.Removed);

        var page = await CreateService().ListAsync(_fund.Id, Filter(), default);

        Assert.Equal(4, page.Total);
        Assert.Equal(["Thủ quỹ chính", "Thủ quỹ", "An", "Bình"], page.Items.Select(item => item.FullName).ToArray());
        Assert.Equal([MemberRole.Owner, MemberRole.Treasurer, MemberRole.Student, MemberRole.Student], page.Items.Select(item => item.Role).ToArray());
    }

    [Fact]
    public async Task List_can_show_removed_members_filter_by_role_and_search_name_or_email()
    {
        SeedStudent("an@x.com", "Nguyễn An");
        SeedStudent("binh@x.com", "Trần Bình");
        SeedStudent("gone@x.com", "Đã Nghỉ", status: MemberStatus.Removed);

        var removed = await CreateService().ListAsync(_fund.Id, Filter(status: MemberStatus.Removed), default);
        Assert.Equal("Đã Nghỉ", Assert.Single(removed.Items).FullName);

        var students = await CreateService().ListAsync(_fund.Id, Filter(role: MemberRole.Student), default);
        Assert.Equal(2, students.Total);

        var byName = await CreateService().ListAsync(_fund.Id, Filter(search: "  TRẦN "), default);
        Assert.Equal("Trần Bình", Assert.Single(byName.Items).FullName);

        var byEmail = await CreateService().ListAsync(_fund.Id, Filter(search: "an@x"), default);
        Assert.Equal("Nguyễn An", Assert.Single(byEmail.Items).FullName);
    }

    [Fact]
    public async Task List_pages_results_and_only_returns_this_funds_members()
    {
        for (var i = 1; i <= 5; i++)
        {
            SeedStudent($"s{i}@x.com", $"HS {i}");
        }

        var other = new Fund("Quỹ khác", _owner.Id, "2026-2027", Now);
        _db.Seed(other);
        SeedStudent("other@x.com", "Ở quỹ khác", fund: other);

        var second = await CreateService().ListAsync(_fund.Id, Filter(page: 2, pageSize: 3), default);

        Assert.Equal(7, second.Total); // owner + treasurer + 5
        Assert.Equal(3, second.Items.Count);
        Assert.DoesNotContain(second.Items, item => item.FullName == "Ở quỹ khác");
    }

    [Fact]
    public async Task List_flags_which_accounts_the_treasurer_may_manage_AZ07()
    {
        SeedStudent("provisioned@x.com", "Do thủ quỹ cấp");
        SeedStudent("tu-dang-ky@x.com", "Tự đăng ký", origin: UserOrigin.SelfRegistered);
        var (shared, _) = SeedStudent("chung@x.com", "Ở hai quỹ");
        var other = new Fund("Quỹ khác", Guid.NewGuid(), "2026-2027", Now);
        _db.Seed(other);
        _db.Seed(new FundMember(other.Id, shared.Id, MemberRole.Student, Now));

        var page = await CreateService().ListAsync(_fund.Id, Filter(role: MemberRole.Student), default);

        var flags = page.Items.ToDictionary(item => item.FullName, item => item.CanManageAccount);
        Assert.True(flags["Do thủ quỹ cấp"]);
        Assert.False(flags["Tự đăng ký"]);
        Assert.False(flags["Ở hai quỹ"]);
    }

    // ---------- Thêm một email ----------

    [Fact]
    public async Task Add_new_email_creates_provisioned_account_with_temporary_password_returned_once()
    {
        var result = await CreateService().AddAsync(_fund.Id, "  Moi@Example.COM ", "Trần Mới", default);

        Assert.Equal(AddMemberOutcome.Created, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.TemporaryPassword));
        Assert.True(PasswordPolicyHolder.Satisfies(result.TemporaryPassword!));

        var user = Assert.Single(_db.Query<User>(), item => item.Email == "moi@example.com");
        Assert.Equal("Trần Mới", user.FullName);
        Assert.Equal(UserOrigin.Provisioned, user.Origin);
        Assert.True(user.MustChangePassword);
        Assert.Equal("h:" + result.TemporaryPassword, user.PasswordHash);

        var member = Assert.Single(_db.Query<FundMember>(), item => item.UserId == user.Id);
        Assert.Equal(MemberRole.Student, member.Role);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.Equal(member.Id, result.Member!.Id);
    }

    [Fact]
    public async Task Add_never_writes_temporary_password_or_hash_to_audit()
    {
        var result = await CreateService().AddAsync(_fund.Id, "moi@example.com", "Trần Mới", default);

        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(MembersAuditActions.Add, log.Action);
        Assert.Equal(FinancialScope.General, log.Scope);
        Assert.Equal(_fund.Id, log.FundId);
        Assert.Equal(_owner.Id, log.ActorUserId);
        var json = (log.BeforeJson ?? "") + (log.AfterJson ?? "");
        Assert.DoesNotContain(result.TemporaryPassword!, json);
        Assert.DoesNotContain("h:", json);
    }

    [Fact]
    public async Task Add_existing_account_only_adds_membership_without_temporary_password_or_changing_the_account()
    {
        var existing = new User("co@example.com", "Đã Có", "h:keep", UserOrigin.SelfRegistered, false, Now);
        _db.Seed(existing);

        var result = await CreateService().AddAsync(_fund.Id, "CO@example.com", "Tên khác bị bỏ qua", default);

        Assert.Equal(AddMemberOutcome.ExistingAdded, result.Outcome);
        Assert.Null(result.TemporaryPassword);
        Assert.Equal("Đã Có", existing.FullName);
        Assert.Equal("h:keep", existing.PasswordHash);
        Assert.Contains(_db.Query<FundMember>(), item => item.UserId == existing.Id && item.Role == MemberRole.Student);
    }

    [Fact]
    public async Task Add_active_member_again_is_409_member_already_exists()
    {
        SeedStudent("hs@x.com");

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().AddAsync(_fund.Id, "HS@x.com", "Học Sinh", default));
        Assert.Equal(MembersErrors.AlreadyExistsCode, error.Code);
    }

    [Fact]
    public async Task Add_removed_member_reactivates_as_student_even_if_they_were_a_treasurer()
    {
        var (user, member) = SeedStudent("cu@x.com", status: MemberStatus.Removed);
        member.Role = MemberRole.Treasurer;
        member.RemovedAt = Now;

        var result = await CreateService().AddAsync(_fund.Id, "cu@x.com", "Học Sinh", default);

        Assert.Equal(AddMemberOutcome.Reactivated, result.Outcome);
        Assert.Null(result.TemporaryPassword);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.Null(member.RemovedAt);
        Assert.Equal(MemberRole.Student, member.Role);
        Assert.Equal(MembersAuditActions.Reactivate, Assert.Single(_db.Query<AuditLog>()).Action);
    }

    [Fact]
    public async Task Add_rejects_invalid_email_and_blank_name_for_new_account()
    {
        var invalid = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddAsync(_fund.Id, "khong-phai-email", "A", default));
        Assert.Equal(MembersErrors.InvalidEmailCode, invalid.Code);

        var blank = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddAsync(_fund.Id, "a@x.com", "   ", default));
        Assert.Equal(MembersErrors.InvalidNameCode, blank.Code);
    }

    [Fact]
    public async Task Add_to_archived_fund_is_refused()
    {
        _fund.Status = FundStatus.Archived;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddAsync(_fund.Id, "a@x.com", "A", default));
        Assert.Equal("fund.archived", error.Code);
        Assert.Empty(_db.Query<User>().Where(user => user.Email == "a@x.com"));
    }

    // ---------- Thêm nhiều email (OQ-20) ----------

    [Fact]
    public async Task AddMany_handles_each_email_independently_with_per_row_outcome()
    {
        _db.Seed(new User("co@x.com", "Đã Có", "h", UserOrigin.SelfRegistered, false, Now));
        SeedStudent("active@x.com");
        SeedStudent("cu@x.com", status: MemberStatus.Removed);

        var result = await CreateService().AddManyAsync(
            _fund.Id,
            Rows("moi@x.com", "CO@x.com", "active@x.com", "cu@x.com", "khong-hop-le", "MOI@x.com", "  ", "moi2@x.com"),
            default);

        var byEmail = result.Results.ToDictionary(item => item.Email);
        Assert.Equal(AddMemberOutcome.Created, byEmail["moi@x.com"].Outcome);
        Assert.Equal(AddMemberOutcome.ExistingAdded, byEmail["co@x.com"].Outcome);
        Assert.Equal(AddMemberOutcome.AlreadyExists, byEmail["active@x.com"].Outcome);
        Assert.Equal(AddMemberOutcome.Reactivated, byEmail["cu@x.com"].Outcome);
        Assert.Equal(AddMemberOutcome.Invalid, byEmail["khong-hop-le"].Outcome);
        Assert.Equal(AddMemberOutcome.Created, byEmail["moi2@x.com"].Outcome);

        // Trùng (MOI@x.com) và dòng trắng bị bỏ: 6 kết quả, thứ tự xuất hiện đầu tiên.
        Assert.Equal(6, result.Results.Count);
        Assert.Equal(["moi@x.com", "co@x.com", "active@x.com", "cu@x.com", "khong-hop-le", "moi2@x.com"], result.Results.Select(item => item.Email).ToArray());
    }

    [Fact]
    public async Task AddMany_returns_temporary_password_only_for_new_accounts_and_never_audits_it()
    {
        _db.Seed(new User("co@x.com", "Đã Có", "h", UserOrigin.SelfRegistered, false, Now));

        var result = await CreateService().AddManyAsync(_fund.Id, Rows("moi@x.com", "co@x.com"), default);

        var created = result.Results.Single(item => item.Outcome == AddMemberOutcome.Created);
        Assert.False(string.IsNullOrEmpty(created.TemporaryPassword));
        Assert.Null(result.Results.Single(item => item.Outcome == AddMemberOutcome.ExistingAdded).TemporaryPassword);

        Assert.Equal(2, _db.Query<AuditLog>().Count()); // mỗi người một bản ghi
        Assert.All(_db.Query<AuditLog>(), log => Assert.DoesNotContain(created.TemporaryPassword!, (log.BeforeJson ?? "") + (log.AfterJson ?? "")));
    }

    [Fact]
    public async Task AddMany_derives_a_provisional_name_from_the_email_local_part()
    {
        await CreateService().AddManyAsync(_fund.Id, Rows("tran.an@x.com"), default);

        Assert.Equal("tran.an", Assert.Single(_db.Query<User>(), item => item.Email == "tran.an@x.com").FullName);
    }

    [Fact]
    public async Task AddMany_rejects_empty_more_than_50_and_archived_fund()
    {
        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddManyAsync(_fund.Id, [], default));
        Assert.Equal(MembersErrors.NoEmailsCode, empty.Code);

        var blanks = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddManyAsync(_fund.Id, Rows("  ", ""), default));
        Assert.Equal(MembersErrors.NoEmailsCode, blanks.Code);

        var tooMany = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().AddManyAsync(_fund.Id, Rows(Enumerable.Range(1, 51).Select(i => $"u{i}@x.com").ToArray()), default));
        Assert.Equal(MembersErrors.TooManyEmailsCode, tooMany.Code);

        _fund.Status = FundStatus.Archived;
        var archived = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().AddManyAsync(_fund.Id, Rows("a@x.com"), default));
        Assert.Equal("fund.archived", archived.Code);
    }

    [Fact]
    public async Task AddMany_accepts_exactly_50_emails()
    {
        var result = await CreateService().AddManyAsync(_fund.Id, Rows(Enumerable.Range(1, 50).Select(i => $"u{i}@x.com").ToArray()), default);

        Assert.Equal(50, result.Results.Count(item => item.Outcome == AddMemberOutcome.Created));
    }

    [Fact]
    public async Task AddMany_uses_the_name_typed_next_to_each_email_and_reports_a_too_long_name_per_row()
    {
        var result = await CreateService().AddManyAsync(
            _fund.Id,
            [new("a@x.com", "  Hồ Đăng Quỳnh "), new("b@x.com", null), new("c@x.com", new string('x', 101))],
            default);

        Assert.Equal("Hồ Đăng Quỳnh", _db.Query<User>().Single(item => item.Email == "a@x.com").FullName);
        Assert.Equal("b", _db.Query<User>().Single(item => item.Email == "b@x.com").FullName);
        var bad = result.Results.Single(item => item.Email == "c@x.com");
        Assert.Equal((AddMemberOutcome.Invalid, MembersErrors.InvalidNameMessage), (bad.Outcome, bad.Message));
        Assert.DoesNotContain(_db.Query<User>(), item => item.Email == "c@x.com");
    }

    [Fact]
    public async Task A_new_or_reactivated_member_still_owes_the_open_general_charges()
    {
        var open = new Charge(_fund.Id, null, "Quỹ HK1", null, 200_000, new DateOnly(2026, 12, 30), ChargeKind.Regular, _owner.Id, Now);
        var closed = new Charge(_fund.Id, null, "Quỹ cũ", null, 100_000, new DateOnly(2026, 10, 30), ChargeKind.Regular, _owner.Id, Now) { Status = ChargeStatus.Closed };
        _db.Seed(open, closed);
        var (user, _) = SeedStudent("cu@x.com", status: MemberStatus.Removed);
        _db.Seed(new ChargeAssignment(open.Id, user.Id, 200_000) { Status = AssignmentStatus.Cancelled });

        await CreateService().AddAsync(_fund.Id, "moi@x.com", "Mới", default);
        await CreateService().AddAsync(_fund.Id, "cu@x.com", "Cũ", default);

        var fresh = _db.Query<User>().Single(item => item.Email == "moi@x.com");
        var freshOwed = Assert.Single(_db.Query<ChargeAssignment>(), item => item.UserId == fresh.Id);
        Assert.Equal((open.Id, 200_000L, AssignmentStatus.Unpaid), (freshOwed.ChargeId, freshOwed.AmountDue, freshOwed.Status));
        var restored = Assert.Single(_db.Query<ChargeAssignment>(), item => item.UserId == user.Id);
        Assert.Equal(AssignmentStatus.Unpaid, restored.Status); // khôi phục, không tạo trùng
    }

    // ---------- Cấp lại mật khẩu tạm: chỉ khi học sinh chưa tự đặt mật khẩu ----------

    [Fact]
    public async Task Reset_password_and_rename_are_blocked_once_the_student_has_set_their_own_password()
    {
        var (_, member) = SeedStudent("da-dang-nhap@x.com", "Đã đăng nhập", passwordSet: true);

        var reset = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ResetPasswordAsync(_fund.Id, member.Id, default));
        var rename = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateAsync(_fund.Id, member.Id, "Tên mới", default));

        Assert.Equal(MembersErrors.PasswordAlreadySetCode, reset.Code);
        Assert.Equal(MembersErrors.PasswordAlreadySetCode, rename.Code);
        Assert.Equal("Đã đăng nhập", _db.Query<User>().Single(item => item.Email == "da-dang-nhap@x.com").FullName);
    }

    [Fact]
    public async Task List_flags_can_manage_only_for_students_who_never_set_a_password()
    {
        SeedStudent("moi@x.com", "Chưa đổi");
        SeedStudent("da-doi@x.com", "Đã đổi", passwordSet: true);

        var page = await CreateService().ListAsync(_fund.Id, Filter(role: MemberRole.Student), default);

        var flags = page.Items.ToDictionary(item => item.FullName, item => item.CanManageAccount);
        Assert.True(flags["Chưa đổi"]);
        Assert.False(flags["Đã đổi"]);
    }

    // ---------- Sửa họ tên ----------

    [Fact]
    public async Task Update_changes_the_name_of_a_provisioned_student_and_audits_before_and_after()
    {
        var (user, member) = SeedStudent("hs@x.com", "Tên cũ");

        var dto = await CreateService().UpdateAsync(_fund.Id, member.Id, "  Tên mới ", default);

        Assert.Equal("Tên mới", dto.FullName);
        Assert.Equal("Tên mới", user.FullName);
        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(MembersAuditActions.Update, log.Action);
        Assert.Contains("Tên cũ", log.BeforeJson);
        Assert.Contains("Tên mới", log.AfterJson);
    }

    [Fact]
    public async Task Update_blank_name_is_rejected()
    {
        var (_, member) = SeedStudent();
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateAsync(_fund.Id, member.Id, "   ", default));
    }

    [Fact]
    public async Task Treasurer_cannot_touch_the_owner_or_another_treasurer_AZ02()
    {
        ActAs(_treasurer);
        var other = new User("t2@x.com", "Thủ quỹ 2", "h", UserOrigin.Provisioned, false, Now);
        var otherMember = new FundMember(_fund.Id, other.Id, MemberRole.Treasurer, Now);
        _db.Seed(other);
        _db.Seed(otherMember);

        foreach (var target in new[] { _ownerMember, otherMember })
        {
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().UpdateAsync(_fund.Id, target.Id, "X", default));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ResetPasswordAsync(_fund.Id, target.Id, default));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RemoveAsync(_fund.Id, target.Id, default));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().GetRemovalPreviewAsync(_fund.Id, target.Id, default));
        }
    }

    [Fact]
    public async Task Treasurer_can_manage_students()
    {
        ActAs(_treasurer);
        var (_, member) = SeedStudent();

        var result = await CreateService().ResetPasswordAsync(_fund.Id, member.Id, default);
        Assert.False(string.IsNullOrEmpty(result.TemporaryPassword));
    }

    [Fact]
    public async Task Owner_can_manage_a_provisioned_treasurer()
    {
        _treasurer.MustChangePassword = true; // chưa tự đặt mật khẩu
        var dto = await CreateService().UpdateAsync(_fund.Id, _treasurerMember.Id, "Thủ quỹ đổi tên", default);
        Assert.Equal("Thủ quỹ đổi tên", dto.FullName);
    }

    [Fact]
    public async Task Cannot_rename_or_reset_a_self_registered_account_AZ07()
    {
        var (_, member) = SeedStudent("tu-dang-ky@x.com", origin: UserOrigin.SelfRegistered);

        var rename = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateAsync(_fund.Id, member.Id, "X", default));
        Assert.Equal(MembersErrors.AccountNotManagedCode, rename.Code);
        var reset = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ResetPasswordAsync(_fund.Id, member.Id, default));
        Assert.Equal(MembersErrors.AccountNotManagedCode, reset.Code);
    }

    [Fact]
    public async Task Cannot_rename_or_reset_an_account_that_is_also_active_in_another_fund_AZ07()
    {
        var (user, member) = SeedStudent("chung@x.com");
        var other = new Fund("Quỹ khác", Guid.NewGuid(), "2026-2027", Now);
        _db.Seed(other);
        _db.Seed(new FundMember(other.Id, user.Id, MemberRole.Student, Now));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ResetPasswordAsync(_fund.Id, member.Id, default));
        Assert.Equal(MembersErrors.AccountNotManagedCode, error.Code);
    }

    [Fact]
    public async Task Removed_membership_in_another_fund_does_not_block_management()
    {
        var (user, member) = SeedStudent("chung@x.com");
        var other = new Fund("Quỹ khác", Guid.NewGuid(), "2026-2027", Now);
        _db.Seed(other);
        _db.Seed(new FundMember(other.Id, user.Id, MemberRole.Student, Now) { Status = MemberStatus.Removed });

        var result = await CreateService().ResetPasswordAsync(_fund.Id, member.Id, default);
        Assert.False(string.IsNullOrEmpty(result.TemporaryPassword));
    }

    [Fact]
    public async Task Cannot_rename_or_reset_yourself_here()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ResetPasswordAsync(_fund.Id, _ownerMember.Id, default));
        Assert.Equal(MembersErrors.CannotActOnSelfCode, error.Code);
    }

    [Fact]
    public async Task Member_of_another_fund_is_not_found_not_forbidden()
    {
        var other = new Fund("Quỹ khác", _owner.Id, "2026-2027", Now);
        _db.Seed(other);
        var (_, foreign) = SeedStudent("khac@x.com", fund: other);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().UpdateAsync(_fund.Id, foreign.Id, "X", default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().ResetPasswordAsync(_fund.Id, foreign.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().RemoveAsync(_fund.Id, foreign.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().UpdateAsync(_fund.Id, Guid.NewGuid(), "X", default));
    }

    // ---------- Cấp lại mật khẩu tạm ----------

    [Fact]
    public async Task Reset_password_sets_new_hash_forces_change_rotates_stamp_and_audits_without_secrets()
    {
        var (user, member) = SeedStudent();
        var oldStamp = user.SecurityStamp;

        var result = await CreateService().ResetPasswordAsync(_fund.Id, member.Id, default);

        Assert.Equal("h:" + result.TemporaryPassword, user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual(oldStamp, user.SecurityStamp);

        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(MembersAuditActions.ResetPassword, log.Action);
        var json = (log.BeforeJson ?? "") + (log.AfterJson ?? "");
        Assert.DoesNotContain(result.TemporaryPassword, json);
        Assert.DoesNotContain(user.SecurityStamp, json);
        Assert.DoesNotContain("h:", json);
    }

    [Fact]
    public async Task Reset_password_twice_gives_different_passwords()
    {
        var (_, member) = SeedStudent();
        var first = await CreateService().ResetPasswordAsync(_fund.Id, member.Id, default);
        var second = await CreateService().ResetPasswordAsync(_fund.Id, member.Id, default);

        Assert.NotEqual(first.TemporaryPassword, second.TemporaryPassword);
    }

    // ---------- Ngừng thành viên (OQ-15, BR-30) ----------

    private (Charge Charge, ChargeAssignment Assignment) SeedAssignment(User user, Fund fund, ChargeStatus chargeStatus, AssignmentStatus status)
    {
        var charge = new Charge(fund.Id, null, "Quỹ lớp", null, 100_000, new DateOnly(2026, 12, 1), ChargeKind.Regular, _owner.Id, Now) { Status = chargeStatus };
        var assignment = new ChargeAssignment(charge.Id, user.Id, 100_000) { Status = status };
        _db.Seed(charge);
        _db.Seed(assignment);
        return (charge, assignment);
    }

    [Fact]
    public async Task Remove_marks_removed_keeps_the_account_and_history_and_audits()
    {
        var (user, member) = SeedStudent();

        var result = await CreateService().RemoveAsync(_fund.Id, member.Id, default);

        Assert.Equal(0, result.CancelledAssignments);
        Assert.Equal(MemberStatus.Removed, member.Status);
        Assert.Equal(Now, member.RemovedAt);
        Assert.Contains(_db.Query<User>(), item => item.Id == user.Id); // BR-30: không xóa
        Assert.Contains(_db.Query<FundMember>(), item => item.Id == member.Id);
        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(MembersAuditActions.Remove, log.Action);
    }

    [Fact]
    public async Task Remove_cancels_only_unpaid_assignments_of_open_charges_in_this_fund_OQ15()
    {
        var (user, member) = SeedStudent();
        var other = new Fund("Quỹ khác", _owner.Id, "2026-2027", Now);
        _db.Seed(other);

        var (_, unpaidOpen) = SeedAssignment(user, _fund, ChargeStatus.Open, AssignmentStatus.Unpaid);
        var (_, paid) = SeedAssignment(user, _fund, ChargeStatus.Open, AssignmentStatus.Paid);
        var (_, unpaidClosed) = SeedAssignment(user, _fund, ChargeStatus.Closed, AssignmentStatus.Unpaid);
        var (_, otherFund) = SeedAssignment(user, other, ChargeStatus.Open, AssignmentStatus.Unpaid);
        var (_, someoneElse) = SeedAssignment(_treasurer, _fund, ChargeStatus.Open, AssignmentStatus.Unpaid);

        var result = await CreateService().RemoveAsync(_fund.Id, member.Id, default);

        Assert.Equal(1, result.CancelledAssignments);
        Assert.Equal(AssignmentStatus.Cancelled, unpaidOpen.Status);
        Assert.Equal(AssignmentStatus.Paid, paid.Status);
        Assert.Equal(AssignmentStatus.Unpaid, unpaidClosed.Status);
        Assert.Equal(AssignmentStatus.Unpaid, otherFund.Status);
        Assert.Equal(AssignmentStatus.Unpaid, someoneElse.Status);
        Assert.Contains("\"cancelledAssignments\":1", Assert.Single(_db.Query<AuditLog>()).AfterJson);
    }

    [Fact]
    public async Task Remove_is_refused_while_the_member_still_has_a_balance()
    {
        var (user, member) = SeedStudent();
        _db.Seed(new PersonalBalance(_fund.Id, user.Id) { Balance = 150_000 });

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemoveAsync(_fund.Id, member.Id, default));

        Assert.Equal(MembersErrors.HasBalanceCode, error.Code);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Remove_is_allowed_once_the_balance_is_zero()
    {
        var (user, member) = SeedStudent();
        _db.Seed(new PersonalBalance(_fund.Id, user.Id) { Balance = 0 });

        await CreateService().RemoveAsync(_fund.Id, member.Id, default);

        Assert.Equal(MemberStatus.Removed, member.Status);
    }

    [Fact]
    public async Task Remove_is_refused_while_a_payment_awaits_confirmation()
    {
        var (user, member) = SeedStudent();
        SeedAssignment(user, _fund, ChargeStatus.Open, AssignmentStatus.AwaitingConfirmation);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemoveAsync(_fund.Id, member.Id, default));

        Assert.Equal(MembersErrors.PendingPaymentsCode, error.Code);
        Assert.Equal(MemberStatus.Active, member.Status);
    }

    [Fact]
    public async Task Owner_cannot_be_removed()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemoveAsync(_fund.Id, _ownerMember.Id, default));
        Assert.Equal(MembersErrors.CannotRemoveOwnerCode, error.Code);
    }

    [Fact]
    public async Task Owner_can_remove_a_treasurer()
    {
        await CreateService().RemoveAsync(_fund.Id, _treasurerMember.Id, default);
        Assert.Equal(MemberStatus.Removed, _treasurerMember.Status);
    }

    [Fact]
    public async Task Remove_twice_is_409()
    {
        var (_, member) = SeedStudent();
        await CreateService().RemoveAsync(_fund.Id, member.Id, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RemoveAsync(_fund.Id, member.Id, default));
        Assert.Equal(MembersErrors.AlreadyRemovedCode, error.Code);
    }

    [Fact]
    public async Task Removed_member_loses_fund_access_BR30()
    {
        var (user, member) = SeedStudent();
        await CreateService().RemoveAsync(_fund.Id, member.Id, default);

        await Assert.ThrowsAsync<NotFoundException>(() => new AccessService(_db).GetFundAccessAsync(user.Id, _fund.Id, default));
    }

    [Fact]
    public async Task Removal_preview_reports_the_impact_without_changing_anything()
    {
        var (user, member) = SeedStudent();
        SeedAssignment(user, _fund, ChargeStatus.Open, AssignmentStatus.Unpaid);
        SeedAssignment(user, _fund, ChargeStatus.Open, AssignmentStatus.Unpaid);

        var preview = await CreateService().GetRemovalPreviewAsync(_fund.Id, member.Id, default);

        Assert.Equal(2, preview.UnpaidCount);
        Assert.Equal(0, preview.PendingPaymentCount);
        Assert.Equal(0, preview.Balance);
        Assert.True(preview.CanRemove);
        Assert.Null(preview.BlockedReason);
        Assert.Equal(MemberStatus.Active, member.Status);
    }

    [Fact]
    public async Task Removal_preview_explains_why_removal_is_blocked()
    {
        var (user, member) = SeedStudent();
        _db.Seed(new PersonalBalance(_fund.Id, user.Id) { Balance = 50_000 });

        var preview = await CreateService().GetRemovalPreviewAsync(_fund.Id, member.Id, default);

        Assert.False(preview.CanRemove);
        Assert.Equal(50_000, preview.Balance);
        Assert.Equal(MembersErrors.HasBalanceMessage, preview.BlockedReason);

        var owner = await CreateService().GetRemovalPreviewAsync(_fund.Id, _ownerMember.Id, default);
        Assert.False(owner.CanRemove);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static class PasswordPolicyHolder
    {
        public static bool Satisfies(string password) => ClassFund.Domain.Rules.PasswordPolicy.IsSatisfiedBy(password);
    }
}
