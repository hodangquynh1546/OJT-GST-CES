using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Storage;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Attachments;

/// <summary>Ảnh đại diện (FR-04, OQ-16): ảnh JPG/PNG/WEBP ≤ 2 MB, không thuộc quỹ nào.</summary>
public interface IAvatarService
{
    /// <summary>Đặt / thay ảnh đại diện của người dùng hiện tại; object ảnh cũ bị xóa khỏi storage.</summary>
    Task<AvatarDto> SetMineAsync(UploadFileInput file, CancellationToken cancellationToken);

    /// <summary>Ảnh của chính mình hoặc của người cùng quỹ (đang hoạt động); không thì 404.</summary>
    Task<FileDownload> GetAsync(Guid userId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class AvatarService(
    IAppDb db,
    ICurrentUser currentUser,
    IFileStorage storage,
    AuditTrail audit,
    TimeProvider clock) : IAvatarService
{
    public async Task<AvatarDto> SetMineAsync(UploadFileInput file, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        var fileName = FileIntake.SafeFileName(file.FileName);
        var storageKey = await FileIntake.ValidateAndSaveAsync(storage, AttachmentKind.Avatar, fundId: null, file, cancellationToken);

        (Guid AttachmentId, string? OldKey) result;
        try
        {
            result = await db.InTransactionAsync(
                async token =>
                {
                    var user = await db.LockAsync<User>(userId, token) ?? throw new NotFoundException();
                    string? oldKey = null;
                    if (user.AvatarAttachmentId is { } oldId)
                    {
                        var old = await db.LockAsync<Attachment>(oldId, token);
                        if (old is not null)
                        {
                            old.OwnerId = AttachmentOwners.Discarded;
                            oldKey = old.StorageKey;
                        }
                    }

                    var attachment = new Attachment(
                        null, AttachmentKind.Avatar, fileName, file.ContentType.Trim().ToLowerInvariant(), file.SizeBytes, storageKey, userId, clock.GetUtcNow())
                    {
                        OwnerId = userId,
                    };
                    db.Add(attachment);
                    var before = new { avatarAttachmentId = user.AvatarAttachmentId };
                    user.AvatarAttachmentId = attachment.Id;
                    audit.Record(new AuditEntry(
                        AttachmentsAuditActions.AvatarUpdate,
                        FundId: null,
                        userId,
                        FinancialScope.System,
                        EntityType: nameof(User),
                        EntityId: userId,
                        Before: before,
                        After: new { avatarAttachmentId = attachment.Id }));
                    await db.SaveChangesAsync(token);
                    return (attachment.Id, oldKey);
                },
                cancellationToken);
        }
        catch
        {
            await FileIntake.TryDeleteAsync(storage, [storageKey], CancellationToken.None);
            throw;
        }

        if (result.OldKey is { } key)
        {
            await FileIntake.TryDeleteAsync(storage, [key], cancellationToken);
        }

        return new AvatarDto(result.AttachmentId);
    }

    public async Task<FileDownload> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var viewerId = currentUser.UserId ?? throw new ForbiddenAccessException();
        if (viewerId != userId)
        {
            var viewerFunds = db.Query<FundMember>()
                .Where(member => member.UserId == viewerId && member.Status == MemberStatus.Active)
                .Select(member => member.FundId);
            var sharesFund = await db.AnyAsync(
                db.Query<FundMember>().Where(member =>
                    member.UserId == userId && member.Status == MemberStatus.Active && viewerFunds.Contains(member.FundId)),
                cancellationToken);
            if (!sharesFund)
            {
                throw new NotFoundException();
            }
        }

        var avatar = await db.FirstOrDefaultAsync(
            from user in db.Query<User>()
            where user.Id == userId && user.AvatarAttachmentId != null
            join attachment in db.Query<Attachment>() on user.AvatarAttachmentId equals (Guid?)attachment.Id
            where attachment.Kind == AttachmentKind.Avatar && attachment.OwnerId == userId
            select new { attachment.StorageKey, attachment.ContentType, attachment.FileName },
            cancellationToken) ?? throw new NotFoundException();

        var content = await storage.OpenReadAsync(avatar.StorageKey, cancellationToken) ?? throw new NotFoundException();
        return new FileDownload(content, avatar.ContentType, avatar.FileName);
    }
}
