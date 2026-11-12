using ClassFund.Application.Common.Paging;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Transactions;

/// <summary>
/// Sổ thu chi (M6): tra cứu theo phạm vi người xem, ghi / sửa / hủy của thủ quỹ. FR-02 → FR-07; BR-01 → BR-07, BR-11, BR-12,
/// BR-14, BR-22; NFR-03, NFR-05, NFR-08, NFR-09. Người không phải thành viên quỹ → 404, thành viên thiếu quyền → 403.
/// </summary>
public interface ITransactionsService
{
    /// <summary>
    /// Mới nhất trước (ngày giao dịch, rồi thời điểm ghi). Người xem công khai: khoản không công khai bị loại trước mọi bộ lọc,
    /// đếm và phân trang; tìm kiếm chỉ trong nội dung. <c>Total</c> đếm sau khi lọc. M7 dùng lại hàm này (TD-109).
    /// </summary>
    Task<PagedResult<TransactionResponse>> ListAsync(Guid fundId, TransactionFilter filter, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">Không có, hoặc là khoản không công khai với người xem công khai.</exception>
    Task<TransactionResponse> GetAsync(Guid fundId, Guid id, CancellationToken cancellationToken);

    /// <summary>Chỉ GVCN, thủ quỹ (<c>Ledger.ViewFull</c>): AuditLog của khoản theo thời gian.</summary>
    Task<IReadOnlyList<TransactionHistoryItem>> GetHistoryAsync(Guid fundId, Guid id, CancellationToken cancellationToken);

    /// <summary>Gửi lại cùng <paramref name="idempotencyKey"/> trong quỹ trả về khoản đã tạo (<c>Replayed = true</c>), kể cả khi đồng thời (BR-12).</summary>
    Task<TransactionResponse> CreateAsync(
        Guid fundId,
        TransactionType type,
        TransactionValues values,
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Không đổi loại (BR-05); khoản đã hủy → <c>transaction.cancelled</c>. Giá trị không đổi thì không ghi AuditLog.</summary>
    Task<TransactionResponse> UpdateAsync(Guid fundId, Guid id, TransactionValues values, CancellationToken cancellationToken);

    /// <summary>Hủy có lý do, chỉ đánh dấu (BR-06); hủy lại → <c>transaction.cancelled</c>.</summary>
    Task<TransactionResponse> CancelAsync(Guid fundId, Guid id, string reason, CancellationToken cancellationToken);
}
