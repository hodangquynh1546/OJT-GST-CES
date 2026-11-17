using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>
/// Sổ thu chi (append-only; sửa sai bằng bút toán đảo, không UPDATE/DELETE).
/// EventId null = quỹ chung, có giá trị = quỹ sự kiện.
/// </summary>
public sealed class LedgerEntry(Guid fundId, Guid? eventId, LedgerType type, LedgerSource source, Guid? categoryId, string title, string? purpose, long amount, DateOnly transactionDate, Guid performedByUserId, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid? EventId { get; init; } = eventId;

    public LedgerType Type { get; init; } = type;

    public LedgerSource Source { get; init; } = source;

    public Guid? CategoryId { get; init; } = categoryId;

    public string Title { get; init; } = title;

    public string? Purpose { get; init; } = purpose;

    public long Amount { get; init; } = amount;

    public DateOnly TransactionDate { get; init; } = transactionDate;

    public Guid PerformedByUserId { get; init; } = performedByUserId;

    /// <summary>Khoản thu sinh từ Payment nào (nếu Source = Payment).</summary>
    public Guid? PaymentId { get; init; }

    public string? TransferCode { get; init; }

    public string? Note { get; init; }

    /// <summary>Thu do thủ quỹ tự ghi nhận: chưa đối chiếu với tiền thực tế nên báo cáo tách riêng (yêu cầu 1.3).</summary>
    public bool IsSelfDeclared { get; init; }

    /// <summary>Bút toán đảo trỏ về dòng bị đảo.</summary>
    public Guid? ReversesEntryId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>
/// File thực tế lưu trên Neon Object Storage (OQ-16): ảnh hóa đơn, bill chuyển khoản, ảnh đại diện, tài liệu.
/// Bảng chỉ giữ metadata + <see cref="StorageKey"/>; nội dung file nằm ở object storage.
/// </summary>
public sealed class Attachment(Guid? fundId, AttachmentKind kind, string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedByUserId, DateTimeOffset uploadedAt) : Entity
{
    /// <summary>Null chỉ với ảnh đại diện (không thuộc quỹ).</summary>
    public Guid? FundId { get; init; } = fundId;

    public AttachmentKind Kind { get; init; } = kind;

    /// <summary>
    /// Đối tượng sở hữu: LedgerEntry (Receipt/Document), Payment (PaymentProof), User (Avatar).
    /// Null = đã tải lên, chưa gắn (upload trước, gắn khi tạo giao dịch).
    /// </summary>
    public Guid? OwnerId { get; set; }

    public string FileName { get; init; } = fileName;

    public string ContentType { get; init; } = contentType;

    public long SizeBytes { get; init; } = sizeBytes;

    public string StorageKey { get; init; } = storageKey;

    public Guid UploadedByUserId { get; init; } = uploadedByUserId;

    public DateTimeOffset UploadedAt { get; init; } = uploadedAt;
}
