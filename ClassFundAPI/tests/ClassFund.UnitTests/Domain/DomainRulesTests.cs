using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Domain;

public class RolePermissionsTests
{
    [Fact]
    public void Only_owner_can_manage_treasurers()
    {
        Assert.Contains(Permissions.TreasurerManage, RolePermissions.For(MemberRole.Owner));
        Assert.DoesNotContain(Permissions.TreasurerManage, RolePermissions.For(MemberRole.Treasurer));
        Assert.DoesNotContain(Permissions.TreasurerManage, RolePermissions.For(MemberRole.Student));
    }

    [Fact]
    public void Granted_treasurer_has_every_management_permission_and_student_permissions()
    {
        var treasurer = RolePermissions.For(MemberRole.Treasurer);
        foreach (var permission in RolePermissions.For(MemberRole.Student))
        {
            Assert.Contains(permission, treasurer);
        }

        Assert.Contains(Permissions.PaymentConfirm, treasurer);
        Assert.Contains(Permissions.SettlementExecute, treasurer);
        Assert.Contains(Permissions.AuditView, treasurer);
    }

    [Fact]
    public void Student_cannot_confirm_payments_or_see_audit()
    {
        var student = RolePermissions.For(MemberRole.Student);
        Assert.DoesNotContain(Permissions.PaymentConfirm, student);
        Assert.DoesNotContain(Permissions.AuditView, student);
        Assert.DoesNotContain(Permissions.ReportView, student);
        Assert.Contains(Permissions.PaymentSubmit, student);
    }

    [Fact]
    public void Only_self_registered_accounts_can_create_funds()
    {
        Assert.Contains(Permissions.FundCreate, RolePermissions.ForAccount(UserOrigin.SelfRegistered));
        Assert.Empty(RolePermissions.ForAccount(UserOrigin.Provisioned));
    }
}

public class EvidenceRuleTests
{
    [Theory]
    [InlineData(500_000, false)]
    [InlineData(500_001, true)]
    [InlineData(100_000, false)]
    public void Evidence_is_required_only_above_500k(long amount, bool required) =>
        Assert.Equal(required, EvidenceRule.IsRequired(amount));

    [Fact]
    public void Missing_evidence_above_threshold_throws()
    {
        var error = Assert.Throws<BusinessRuleException>(() => EvidenceRule.EnsureProvided(600_000, hasEvidence: false));
        Assert.Equal(EvidenceRule.RequiredCode, error.Code);
        EvidenceRule.EnsureProvided(500_000, hasEvidence: false);
        EvidenceRule.EnsureProvided(600_000, hasEvidence: true);
    }

}

public class FileRuleTests
{
    private const long Mb = 1024 * 1024;

    [Theory]
    [InlineData(AttachmentKind.Receipt, "image/png", 1024, true)]
    [InlineData(AttachmentKind.PaymentProof, "application/pdf", 5 * Mb, true)]
    [InlineData(AttachmentKind.PaymentProof, "application/pdf", 5 * Mb + 1, false)]
    [InlineData(AttachmentKind.Receipt, "text/html", 1024, false)]
    [InlineData(AttachmentKind.Receipt, "image/jpeg", 0, false)]
    [InlineData(AttachmentKind.Avatar, "image/webp", 2 * Mb, true)]
    [InlineData(AttachmentKind.Avatar, "application/pdf", 1024, false)]
    [InlineData(AttachmentKind.Avatar, "image/png", 2 * Mb + 1, false)]
    [InlineData(AttachmentKind.Document, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 10 * Mb, true)]
    [InlineData(AttachmentKind.Document, "application/zip", 1024, false)]
    public void File_type_and_size_are_limited_per_kind(AttachmentKind kind, string type, long size, bool allowed)
    {
        if (allowed)
        {
            FileRule.EnsureAllowed(kind, type, size);
        }
        else
        {
            Assert.Throws<BusinessRuleException>(() => FileRule.EnsureAllowed(kind, type, size));
        }
    }

    [Fact]
    public void Every_kind_has_a_limit()
    {
        foreach (var kind in Enum.GetValues<AttachmentKind>())
        {
            Assert.True(FileRule.Limits.ContainsKey(kind));
        }
    }
}

public class WithdrawalRuleTests
{
    [Fact]
    public void Withdrawal_requests_the_whole_balance() =>
        Assert.Equal(150_000, WithdrawalRule.AmountToRequest(150_000, hasPendingRequest: false));

    [Fact]
    public void Cannot_request_twice_or_with_zero_balance()
    {
        Assert.Equal(WithdrawalRule.PendingExistsCode,
            Assert.Throws<BusinessRuleException>(() => WithdrawalRule.AmountToRequest(150_000, hasPendingRequest: true)).Code);
        Assert.Equal(WithdrawalRule.NothingToWithdrawCode,
            Assert.Throws<BusinessRuleException>(() => WithdrawalRule.AmountToRequest(0, hasPendingRequest: false)).Code);
    }

    [Fact]
    public void Balance_pending_withdrawal_cannot_be_spent()
    {
        Assert.Throws<BusinessRuleException>(() => WithdrawalRule.EnsureCanSpend(hasPendingRequest: true));
        WithdrawalRule.EnsureCanSpend(hasPendingRequest: false);
    }
}

public class YearEndRuleTests
{
    [Fact]
    public void Year_end_requires_active_fund_and_no_unsettled_events()
    {
        YearEndRule.EnsureCanClose(FundStatus.Active, unsettledEventCount: 0);
        Assert.Equal(YearEndRule.OpenEventsCode,
            Assert.Throws<BusinessRuleException>(() => YearEndRule.EnsureCanClose(FundStatus.Active, 1)).Code);
        Assert.Equal(YearEndRule.AlreadyClosedCode,
            Assert.Throws<BusinessRuleException>(() => YearEndRule.EnsureCanClose(FundStatus.Archived, 0)).Code);
    }

    [Fact]
    public void Archived_fund_accepts_no_new_money() =>
        Assert.Throws<BusinessRuleException>(() => YearEndRule.EnsureFundActive(FundStatus.Archived));
}

public class PaymentConfirmationPolicyTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Treasurer_confirming_own_payment_is_self_declared() =>
        Assert.Equal(new ConfirmationDecision(true, ConfirmationMode.SelfDeclared), PaymentConfirmationPolicy.Decide(Actor, Actor, actorCanConfirm: true));

    [Fact]
    public void Treasurer_confirming_a_student_is_verified() =>
        Assert.Equal(new ConfirmationDecision(true, ConfirmationMode.Verified), PaymentConfirmationPolicy.Decide(Actor, Guid.NewGuid(), actorCanConfirm: true));

    [Fact]
    public void Actor_without_confirm_permission_is_refused_even_for_self() =>
        Assert.False(PaymentConfirmationPolicy.Decide(Actor, Actor, actorCanConfirm: false).Allowed);
}

public class EventStatusRulesTests
{
    [Theory]
    [InlineData(EventStatus.Open, EventStatus.Completed, true)]
    [InlineData(EventStatus.Completed, EventStatus.Settled, true)]
    [InlineData(EventStatus.Open, EventStatus.Cancelled, true)]
    [InlineData(EventStatus.Open, EventStatus.Settled, false)]
    [InlineData(EventStatus.Completed, EventStatus.Open, false)]
    [InlineData(EventStatus.Completed, EventStatus.Cancelled, false)]
    [InlineData(EventStatus.Settled, EventStatus.Open, false)]
    [InlineData(EventStatus.Cancelled, EventStatus.Open, false)]
    public void Transitions_follow_the_lifecycle(EventStatus from, EventStatus to, bool allowed) =>
        Assert.Equal(allowed, EventStatusRules.CanTransition(from, to));

    [Theory]
    [InlineData(EventStatus.Open, true)]
    [InlineData(EventStatus.Completed, false)]
    [InlineData(EventStatus.Settled, false)]
    [InlineData(EventStatus.Cancelled, false)]
    public void Only_open_events_accept_new_charges_and_expenses(EventStatus status, bool expected) =>
        Assert.Equal(expected, EventStatusRules.AcceptsNewChargesAndExpenses(status));

    [Theory]
    [InlineData(EventStatus.Open, true)]
    [InlineData(EventStatus.Completed, true)]
    [InlineData(EventStatus.Settled, false)]
    [InlineData(EventStatus.Cancelled, false)]
    public void Payments_are_accepted_until_settlement(EventStatus status, bool expected) =>
        Assert.Equal(expected, EventStatusRules.AcceptsPayments(status));
}

public class SpendingRuleTests
{
    [Theory]
    [InlineData(3_500_000, 3_500_000)]
    [InlineData(1, 3_500_000)]
    public void Spending_within_balance_is_allowed(long amount, long balance) =>
        SpendingRule.EnsureWithinBalance(amount, balance);

    [Fact]
    public void Spending_above_balance_is_refused()
    {
        var error = Assert.Throws<BusinessRuleException>(() => SpendingRule.EnsureWithinBalance(3_600_000, 3_500_000));
        Assert.Equal(SpendingRule.ExceedsBalanceCode, error.Code);
    }
}

public class SmallRulesTests
{
    [Theory]
    [InlineData("abc12345", true)]
    [InlineData("abcdefgh", false)]
    [InlineData("12345678", false)]
    [InlineData("a1", false)]
    public void Password_policy(string password, bool ok) => Assert.Equal(ok, PasswordPolicy.IsSatisfiedBy(password));

    [Theory]
    [InlineData("A@b.com", true)]
    [InlineData("no-at.com", false)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.com", false)]
    public void Email_validation(string email, bool ok) => Assert.Equal(ok, EmailAddress.IsValid(email));

    [Fact]
    public void Email_is_normalized_lowercase() => Assert.Equal("a@b.com", EmailAddress.Normalize("  A@B.com "));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Money_must_be_positive(long amount) => Assert.Throws<BusinessRuleException>(() => MoneyRule.EnsureValid(amount));

    [Fact]
    public void Vietnam_date_uses_utc_plus_7() =>
        Assert.Equal(new DateOnly(2026, 11, 17), VietnamTime.DateOf(new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero)));
}
