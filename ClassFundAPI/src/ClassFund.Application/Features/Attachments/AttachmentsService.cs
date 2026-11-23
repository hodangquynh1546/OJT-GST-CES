using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Attachments;

/// <summary>Chứng từ / minh chứng / tài liệu của quỹ trên Neon Object Storage (FR-15, FR-17, BR-07, AZ-05).</summary>
public interface IAttachmentsService
{
    /// <summary>
    /// Tải file lên trước, gắn sau (<see cref="AttachToAsync"/>). Receipt / Document cần <c>Ledger.Manage</c>,
    /// PaymentProof cần <c>Payment.Submit</c>. Đồng thời dọn file chưa gắn quá 24h của quỹ.
    /// </summary>
    Task<AttachmentDto> UploadAsync(Guid fundId, AttachmentKind kind, UploadFileInput file, CancellationToken cancellationToken);

    /// <summary>Stream file qua API. Không có quyền xem (AZ-05) trả 404 như không tồn tại.</summary>
    Task<FileDownload> DownloadAsync(Guid fundId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Cho M6 / M7: gắn các file đã tải lên vào đối tượng sở hữu (LedgerEntry, Payment).
    /// Mỗi file phải thuộc quỹ, chưa gắn, do chính người dùng hiện tại tải lên, đúng loại – không thì 422 <c>attachment.unusable</c>.
    /// <b>Chỉ gọi trong <c>IAppDb.InTransactionAsync</c> của module gọi</b>; không tự SaveChanges.
    /// </summary>
    /// <returns>Số file đã gắn (dùng cho <c>EvidenceRule.EnsureProvided</c>).</returns>
    Task<int> AttachToAsync(
        Guid fundId,
        IReadOnlyCollection<Guid> attachmentIds,
        Guid ownerId,
        IReadOnlySet<AttachmentKind> allowedKinds,
        CancellationToken cancellationToken);

    /// <summary>Danh sách file đã gắn vào một đối tượng (để M6 / M7 trả kèm chi tiết).</summary>
    Task<IReadOnlyList<AttachmentDto>> ListByOwnerAsync(Guid fundId, Guid ownerId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class AttachmentsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    IFileStorage storage,
    AuditTrail audit,
    TimeProvider clock) : IAttachmentsService
{
    /// <summary>File tải lên mà không gắn sau khoảng này thì bị dọn (TD-01).</summary>
    public static readonly TimeSpan UnattachedLifetime = TimeSpan.FromHours(24);

    /// <summary>Mỗi lần upload dọn tối đa chừng này file để không làm chậm request.</summary>
    public const int CleanupBatchSize = 20;

    public async Task<AttachmentDto> UploadAsync(Guid fundId, AttachmentKind kind, UploadFileInput file, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        var required = kind switch
        {
            AttachmentKind.Receipt or AttachmentKind.Document => Permissions.LedgerManage,
            AttachmentKind.PaymentProof => Permissions.PaymentConfirm,
            _ => throw new BusinessRuleException(FileRule.InvalidFileCode, "Loại file không được tải lên ở đây."),
        };
        if (!access.Permissions.Contains(required))
        {
            throw new ForbiddenAccessException();
        }

        var status = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(fund => fund.Id == fundId).Select(fund => (FundStatus?)fund.Status),
            cancellationToken) ?? throw new NotFoundException();
        YearEndRule.EnsureFundActive(status);

        await CleanupStaleAsync(fundId, userId, cancellationToken);

        var fileName = FileIntake.SafeFileName(file.FileName);
        var storageKey = await FileIntake.ValidateAndSaveAsync(storage, kind, fundId, file, cancellationToken);
        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    var attachment = new Attachment(
                        fundId, kind, fileName, file.ContentType.Trim().ToLowerInvariant(), file.SizeBytes, storageKey, userId, clock.GetUtcNow());
                    db.Add(attachment);
                    audit.Record(new AuditEntry(
                        AttachmentsAuditActions.Upload,
                        fundId,
                        userId,
                        FinancialScope.General,
                        EntityType: nameof(Attachment),
                        EntityId: attachment.Id,
                        After: new { kind = kind.ToString(), attachment.FileName, attachment.ContentType, attachment.SizeBytes }));
                    await db.SaveChangesAsync(token);
                    return ToDto(attachment);
                },
                cancellationToken);
        }
        catch
        {
            // Không ghi được metadata → xóa object vừa lưu để không sinh file mồ côi.
            await FileIntake.TryDeleteAsync(storage, [storageKey], CancellationToken.None);
            throw;
        }
    }

    public async Task<FileDownload> DownloadAsync(Guid fundId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        var attachment = await db.FirstOrDefaultAsync(
            db.Query<Attachment>().Where(item => item.Id == attachmentId && item.FundId == fundId),
            cancellationToken);

        if (attachment is null
            || attachment.OwnerId == AttachmentOwners.Discarded
            || !await CanViewAsync(attachment, userId, access, cancellationToken))
        {
            throw new NotFoundException();
        }

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken) ?? throw new NotFoundException();
        return new FileDownload(content, attachment.ContentType, attachment.FileName);
    }

    public async Task<int> AttachToAsync(
        Guid fundId,
        IReadOnlyCollection<Guid> attachmentIds,
        Guid ownerId,
        IReadOnlySet<AttachmentKind> allowedKinds,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var ids = attachmentIds.Distinct().ToArray();
        foreach (var id in ids)
        {
            var attachment = await db.LockAsync<Attachment>(id, cancellationToken);
            if (attachment is null
                || attachment.FundId != fundId
                || attachment.OwnerId is not null
                || attachment.UploadedByUserId != userId
                || !allowedKinds.Contains(attachment.Kind))
            {
                throw new BusinessRuleException(AttachmentsErrors.Unusable, AttachmentsErrors.UnusableMessage);
            }

            attachment.OwnerId = ownerId;
        }

        return ids.Length;
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListByOwnerAsync(Guid fundId, Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(
            db.Query<Attachment>()
                .Where(item => item.FundId == fundId && item.OwnerId == ownerId)
                .OrderBy(item => item.UploadedAt),
            cancellationToken);
        return rows.Select(ToDto).ToArray();
    }

    /// <summary>
    /// AZ-05 / FR-17: thủ quỹ xem tất cả; học sinh xem file mình tải lên, minh chứng thanh toán của chính mình
    /// và chứng từ chi công khai (quỹ chung: mọi thành viên; sự kiện: người đã nhận tham gia).
    /// </summary>
    private async Task<bool> CanViewAsync(Attachment attachment, Guid userId, FundAccess access, CancellationToken cancellationToken)
    {
        if (access.Permissions.Contains(Permissions.LedgerManage) || access.Permissions.Contains(Permissions.PaymentConfirm)
            || attachment.UploadedByUserId == userId)
        {
            return true;
        }

        if (attachment.OwnerId is not { } ownerId)
        {
            return false;
        }

        switch (attachment.Kind)
        {
            case AttachmentKind.PaymentProof:
                return await db.AnyAsync(
                    db.Query<Payment>().Where(payment => payment.Id == ownerId && payment.PayerUserId == userId),
                    cancellationToken);

            case AttachmentKind.Receipt or AttachmentKind.Document:
                var entry = await db.FirstOrDefaultAsync(
                    db.Query<LedgerEntry>()
                        .Where(item => item.Id == ownerId && item.FundId == attachment.FundId)
                        .Select(item => new { item.EventId }),
                    cancellationToken);
                if (entry is null)
                {
                    return false;
                }

                return entry.EventId is not { } eventId
                    || await db.AnyAsync(
                        db.Query<EventParticipant>().Where(participant =>
                            participant.EventId == eventId
                            && participant.UserId == userId
                            && participant.Response == ParticipationResponse.Accepted),
                        cancellationToken);

            default:
                return false;
        }
    }

    /// <summary>Dọn file chưa gắn quá 24h của quỹ (TD-01): đánh dấu Discarded trong DB rồi xóa object.</summary>
    private async Task CleanupStaleAsync(Guid fundId, Guid actorId, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - UnattachedLifetime;
        var keys = await db.InTransactionAsync(
            async token =>
            {
                var stale = await db.ToListAsync(
                    db.Query<Attachment>()
                        .Where(item => item.FundId == fundId && item.OwnerId == null && item.UploadedAt < cutoff)
                        .OrderBy(item => item.UploadedAt)
                        .Take(CleanupBatchSize),
                    token);
                if (stale.Count == 0)
                {
                    return Array.Empty<string>();
                }

                foreach (var item in stale)
                {
                    item.OwnerId = AttachmentOwners.Discarded;
                }

                audit.Record(new AuditEntry(
                    AttachmentsAuditActions.Cleanup,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(Attachment),
                    After: new { count = stale.Count, ids = stale.Select(item => item.Id).ToArray() },
                    Note: "Dọn file tải lên quá 24 giờ chưa gắn vào giao dịch."));
                await db.SaveChangesAsync(token);
                return stale.Select(item => item.StorageKey).ToArray();
            },
            cancellationToken);

        await FileIntake.TryDeleteAsync(storage, keys, cancellationToken);
    }

    public static AttachmentDto ToDto(Attachment attachment) =>
        new(attachment.Id, attachment.Kind, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAt);

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
