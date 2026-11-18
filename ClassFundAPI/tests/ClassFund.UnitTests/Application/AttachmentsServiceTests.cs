using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Attachments;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

public class AttachmentsServiceTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly IReadOnlySet<AttachmentKind> ProofKinds = new HashSet<AttachmentKind> { AttachmentKind.PaymentProof };
    private static readonly IReadOnlySet<AttachmentKind> LedgerKinds = new HashSet<AttachmentKind> { AttachmentKind.Receipt, AttachmentKind.Document };

    private readonly ServiceTestContext _ctx = new();

    private AttachmentsService Service => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Storage, _ctx.Audit, _ctx.Clock);

    private AvatarService Avatars => new(_ctx.Db, _ctx.CurrentUser, _ctx.Storage, _ctx.Audit, _ctx.Clock);

    public static UploadFileInput Png(string name = "bill chuyển khoản.png", int size = 2048) =>
        File(name, "image/png", [.. PngHeader, .. new byte[size - PngHeader.Length]]);

    public static UploadFileInput Pdf(string name = "hoa-don.pdf") => File(name, "application/pdf", [.. "%PDF-1.7\n"u8, .. new byte[512]]);

    private static UploadFileInput File(string name, string contentType, byte[] bytes) =>
        new(name, contentType, bytes.Length, new MemoryStream(bytes));

    [Fact]
    public async Task Student_uploads_payment_proof_unattached_with_random_storage_key_BR07()
    {
        _ctx.ActAs(_ctx.StudentA);

        var dto = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.PaymentProof, Png(), default);

        var attachment = Assert.Single(_ctx.Db.Query<Attachment>());
        Assert.Equal(dto.Id, attachment.Id);
        Assert.Null(attachment.OwnerId);
        Assert.Equal("bill chuyển khoản.png", attachment.FileName);
        Assert.StartsWith($"paymentproof/{_ctx.Fund.Id:N}/", attachment.StorageKey);
        Assert.DoesNotContain("bill", attachment.StorageKey);
        Assert.True(_ctx.Storage.Objects.ContainsKey(attachment.StorageKey));
        Assert.Single(_ctx.AuditLogs, entry => entry.Action == AttachmentsAuditActions.Upload);
    }

    [Fact]
    public async Task Student_cannot_upload_receipt_403()
    {
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default));
    }

    [Fact]
    public async Task Outsider_upload_is_404() =>
        await AsOutsider(() => Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default));

    [Fact]
    public async Task Oversized_receipt_is_rejected_422()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Png(size: (5 * 1024 * 1024) + 1), default));
        Assert.Equal(FileRule.InvalidFileCode, error.Code);
        Assert.Empty(_ctx.Storage.Objects);
    }

    [Fact]
    public async Task Content_not_matching_declared_type_is_rejected()
    {
        var fake = File("virus.png", "image/png", [.. "MZ this is not a png"u8]);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, fake, default));
        Assert.Equal(FileRule.InvalidFileCode, error.Code);
        Assert.Empty(_ctx.Storage.Objects);
    }

    [Fact]
    public async Task Upload_blocked_when_fund_archived_BR37()
    {
        _ctx.Fund.Status = FundStatus.Archived;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default));
        Assert.Equal(YearEndRule.FundArchivedCode, error.Code);
    }

    [Fact]
    public async Task Student_cannot_download_other_students_payment_proof_AZ05()
    {
        _ctx.ActAs(_ctx.StudentA);
        var proof = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.PaymentProof, Png(), default);
        var payment = SeedPayment(_ctx.StudentA.Id);
        await Service.AttachToAsync(_ctx.Fund.Id, [proof.Id], payment.Id, ProofKinds, default);

        _ctx.ActAs(_ctx.StudentB);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(_ctx.Fund.Id, proof.Id, default));

        _ctx.ActAs(_ctx.StudentA);
        await using (var own = (await Service.DownloadAsync(_ctx.Fund.Id, proof.Id, default)).Content)
        {
            Assert.True(own.Length > 0);
        }

        _ctx.ActAs(_ctx.Treasurer);
        var forTreasurer = await Service.DownloadAsync(_ctx.Fund.Id, proof.Id, default);
        Assert.Equal("image/png", forTreasurer.ContentType);
        await forTreasurer.Content.DisposeAsync();
    }

    [Fact]
    public async Task Student_sees_general_expense_receipt_but_not_event_receipt_unless_participant_FR17()
    {
        var general = await UploadAttachedReceiptAsync(eventId: null);
        var eventId = Guid.NewGuid();
        var eventReceipt = await UploadAttachedReceiptAsync(eventId);

        _ctx.ActAs(_ctx.StudentA);
        (await Service.DownloadAsync(_ctx.Fund.Id, general, default)).Content.Dispose();
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(_ctx.Fund.Id, eventReceipt, default));

        _ctx.Db.Seed(new EventParticipant(eventId, _ctx.StudentA.Id) { Response = ParticipationResponse.Accepted });
        (await Service.DownloadAsync(_ctx.Fund.Id, eventReceipt, default)).Content.Dispose();
    }

    [Fact]
    public async Task Unattached_upload_of_someone_else_is_hidden_from_students()
    {
        var receipt = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(_ctx.Fund.Id, receipt.Id, default));
    }

    [Fact]
    public async Task Attachment_of_other_fund_is_404()
    {
        var receipt = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);
        var otherFund = new Fund("Khác", _ctx.Owner.Id, "2026-2027", ServiceTestContext.Start);
        _ctx.Db.Seed(otherFund);
        _ctx.Db.Seed(new FundMember(otherFund.Id, _ctx.Owner.Id, MemberRole.Owner, ServiceTestContext.Start));

        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(otherFund.Id, receipt.Id, default));
    }

    [Fact]
    public async Task Attach_rejects_reuse_wrong_kind_and_foreign_uploads()
    {
        var receipt = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);
        var entryId = Guid.NewGuid();

        await AssertUnusable(() => Service.AttachToAsync(_ctx.Fund.Id, [receipt.Id], entryId, ProofKinds, default));
        _ctx.ActAs(_ctx.Treasurer);
        await AssertUnusable(() => Service.AttachToAsync(_ctx.Fund.Id, [receipt.Id], entryId, LedgerKinds, default));
        _ctx.ActAs(_ctx.Owner);
        Assert.Equal(1, await Service.AttachToAsync(_ctx.Fund.Id, [receipt.Id, receipt.Id], entryId, LedgerKinds, default));
        await AssertUnusable(() => Service.AttachToAsync(_ctx.Fund.Id, [receipt.Id], Guid.NewGuid(), LedgerKinds, default));
        await AssertUnusable(() => Service.AttachToAsync(_ctx.Fund.Id, [Guid.NewGuid()], entryId, LedgerKinds, default));

        Assert.Equal(receipt.Id, Assert.Single(await Service.ListByOwnerAsync(_ctx.Fund.Id, entryId, default)).Id);
    }

    [Fact]
    public async Task Unattached_files_older_than_24h_are_cleaned_on_next_upload_TD01()
    {
        var stale = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);
        var staleKey = _ctx.Db.Query<Attachment>().Single(item => item.Id == stale.Id).StorageKey;

        _ctx.Clock.Now += TimeSpan.FromHours(25);
        await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);

        Assert.Equal(AttachmentOwners.Discarded, _ctx.Db.Query<Attachment>().Single(item => item.Id == stale.Id).OwnerId);
        Assert.False(_ctx.Storage.Objects.ContainsKey(staleKey));
        Assert.Single(_ctx.AuditLogs, entry => entry.Action == AttachmentsAuditActions.Cleanup);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.DownloadAsync(_ctx.Fund.Id, stale.Id, default));
        await AssertUnusable(() => Service.AttachToAsync(_ctx.Fund.Id, [stale.Id], Guid.NewGuid(), LedgerKinds, default));
    }

    [Fact]
    public async Task Avatar_must_be_small_image_TC_M5_01()
    {
        var pdf = await Assert.ThrowsAsync<BusinessRuleException>(() => Avatars.SetMineAsync(Pdf("avatar.pdf"), default));
        Assert.Equal(FileRule.InvalidFileCode, pdf.Code);
        var big = await Assert.ThrowsAsync<BusinessRuleException>(() => Avatars.SetMineAsync(Png(size: (2 * 1024 * 1024) + 1), default));
        Assert.Equal(FileRule.InvalidFileCode, big.Code);
        Assert.Empty(_ctx.Storage.Objects);
    }

    [Fact]
    public async Task Replacing_avatar_discards_old_object_and_others_in_same_fund_can_view()
    {
        _ctx.ActAs(_ctx.StudentA);
        var first = await Avatars.SetMineAsync(Png("me.png"), default);
        var firstKey = _ctx.Db.Query<Attachment>().Single(item => item.Id == first.AttachmentId).StorageKey;
        Assert.StartsWith("avatar/users/", firstKey);

        var second = await Avatars.SetMineAsync(Png("me2.png"), default);

        Assert.Equal(second.AttachmentId, _ctx.StudentA.AvatarAttachmentId);
        Assert.False(_ctx.Storage.Objects.ContainsKey(firstKey));
        Assert.Equal(AttachmentOwners.Discarded, _ctx.Db.Query<Attachment>().Single(item => item.Id == first.AttachmentId).OwnerId);
        Assert.Equal(2, _ctx.AuditLogs.Count(entry => entry.Action == AttachmentsAuditActions.AvatarUpdate));

        _ctx.ActAs(_ctx.StudentB);
        (await Avatars.GetAsync(_ctx.StudentA.Id, default)).Content.Dispose();

        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => Avatars.GetAsync(_ctx.StudentA.Id, default));
    }

    [Fact]
    public async Task User_without_avatar_is_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Avatars.GetAsync(_ctx.Owner.Id, default));
    }

    private async Task<Guid> UploadAttachedReceiptAsync(Guid? eventId)
    {
        _ctx.ActAs(_ctx.Owner);
        var receipt = await Service.UploadAsync(_ctx.Fund.Id, AttachmentKind.Receipt, Pdf(), default);
        var entry = new LedgerEntry(_ctx.Fund.Id, eventId, LedgerType.Expense, LedgerSource.Manual, null, "Chi", null, 600_000, new DateOnly(2026, 11, 18), _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(entry);
        await Service.AttachToAsync(_ctx.Fund.Id, [receipt.Id], entry.Id, LedgerKinds, default);
        return receipt.Id;
    }

    private Payment SeedPayment(Guid payerId)
    {
        var payment = new Payment(Guid.NewGuid(), payerId, 50_000, PaymentMethod.BankTransfer, ServiceTestContext.Start);
        _ctx.Db.Seed(payment);
        return payment;
    }

    private async Task AsOutsider(Func<Task> action)
    {
        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private static async Task AssertUnusable(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(action);
        Assert.Equal(AttachmentsErrors.Unusable, error.Code);
    }
}
