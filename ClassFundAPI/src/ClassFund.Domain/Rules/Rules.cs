using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Domain.Rules;

/// <summary>Ngày nghiệp vụ theo giờ Việt Nam (UTC+7, không có giờ mùa hè).</summary>
public static class VietnamTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly DateOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToOffset(Offset).DateTime);
}

/// <summary>Mật khẩu 8–128 ký tự, có cả chữ và số.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const string ViolationCode = "auth.password_policy";
    public const string ViolationMessage = "Mật khẩu cần 8–128 ký tự, có cả chữ và số.";

    public static bool IsSatisfiedBy(string password) =>
        password.Length is >= MinLength and <= MaxLength
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}

/// <summary>Email là định danh đăng nhập: chuẩn hóa chữ thường, bỏ khoảng trắng.</summary>
public static class EmailAddress
{
    public const int MaxLength = 254;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValid(string email)
    {
        var value = Normalize(email);
        if (value.Length is 0 or > MaxLength || value.Contains(' '))
        {
            return false;
        }

        var at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && value.IndexOf('.', at) > at + 1 && !value.EndsWith('.');
    }
}

/// <summary>Số tiền VND là số nguyên dương (không có phần lẻ).</summary>
public static class MoneyRule
{
    public const long MaxAmount = 100_000_000_000;
    public const string InvalidCode = "money.invalid";

    public static void EnsureValid(long amount)
    {
        if (amount is <= 0 or > MaxAmount)
        {
            throw new BusinessRuleException(InvalidCode, "Số tiền phải lớn hơn 0.");
        }
    }
}

/// <summary>Chứng từ giao dịch: &gt; 500.000đ bắt buộc, ≤ 500.000đ tùy chọn (yêu cầu 6.4).</summary>
public static class EvidenceRule
{
    public const long RequiredAboveAmount = 500_000;
    public const string RequiredCode = "evidence.required";

    public static bool IsRequired(long amount) => amount > RequiredAboveAmount;

    public static void EnsureProvided(long amount, bool hasEvidence)
    {
        if (IsRequired(amount) && !hasEvidence)
        {
            throw new BusinessRuleException(RequiredCode, "Giao dịch trên 500.000đ bắt buộc có chứng từ.");
        }
    }
}

/// <summary>Loại file và dung lượng cho phép theo từng loại file lưu trên object storage (OQ-16).</summary>
public static class FileRule
{
    public const string InvalidFileCode = "file.invalid";
    private const long Mb = 1024 * 1024;

    private static readonly string[] Images = ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] Pdf = ["application/pdf"];
    private static readonly string[] Office =
    [
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ];

    public sealed record Limit(IReadOnlySet<string> ContentTypes, long MaxBytes, string Description);

    public static readonly IReadOnlyDictionary<AttachmentKind, Limit> Limits = new Dictionary<AttachmentKind, Limit>
    {
        [AttachmentKind.Receipt] = new(Set([.. Images, .. Pdf]), 5 * Mb, "ảnh JPG/PNG/WEBP hoặc PDF, tối đa 5 MB"),
        [AttachmentKind.PaymentProof] = new(Set([.. Images, .. Pdf]), 5 * Mb, "ảnh JPG/PNG/WEBP hoặc PDF, tối đa 5 MB"),
        [AttachmentKind.Avatar] = new(Set(Images), 2 * Mb, "ảnh JPG/PNG/WEBP, tối đa 2 MB"),
        [AttachmentKind.Document] = new(Set([.. Images, .. Pdf, .. Office]), 10 * Mb, "PDF, Word, Excel hoặc ảnh, tối đa 10 MB"),
    };

    public static void EnsureAllowed(AttachmentKind kind, string contentType, long sizeBytes)
    {
        var limit = Limits[kind];
        if (!limit.ContentTypes.Contains(contentType) || sizeBytes <= 0 || sizeBytes > limit.MaxBytes)
        {
            throw new BusinessRuleException(InvalidFileCode, $"File phải là {limit.Description}.");
        }
    }

    private static HashSet<string> Set(IEnumerable<string> values) => new(values, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Rút số dư cá nhân (OQ-11): rút một lần TOÀN BỘ số dư; mỗi người chỉ có một yêu cầu đang chờ;
/// số dư đang chờ rút không được dùng để đóng khoản thu.
/// </summary>
public static class WithdrawalRule
{
    public const string NothingToWithdrawCode = "withdrawal.nothing_to_withdraw";
    public const string PendingExistsCode = "withdrawal.pending_exists";
    public const string BalanceLockedCode = "balance.locked_by_withdrawal";

    /// <returns>Số tiền yêu cầu = toàn bộ số dư.</returns>
    public static long AmountToRequest(long currentBalance, bool hasPendingRequest)
    {
        if (hasPendingRequest)
        {
            throw new BusinessRuleException(PendingExistsCode, "Bạn đã có yêu cầu rút tiền đang chờ thủ quỹ xử lý.");
        }

        return currentBalance > 0
            ? currentBalance
            : throw new BusinessRuleException(NothingToWithdrawCode, "Số dư bằng 0, không có tiền để rút.");
    }

    public static void EnsureCanSpend(bool hasPendingRequest)
    {
        if (hasPendingRequest)
        {
            throw new BusinessRuleException(BalanceLockedCode, "Số dư đang chờ rút, không thể dùng để đóng khoản thu.");
        }
    }
}

/// <summary>
/// Tổng kết cuối năm quỹ chung (OQ-13): quyết toán như sự kiện nhưng chia cho cả lớp, làm MỘT lần;
/// chỉ khi quỹ Active và mọi sự kiện đã Settled/Cancelled; xong thì quỹ Archived (chỉ đọc, vẫn rút được số dư).
/// </summary>
public static class YearEndRule
{
    public const string OpenEventsCode = "year_end.open_events";
    public const string AlreadyClosedCode = "year_end.already_closed";
    public const string FundArchivedCode = "fund.archived";

    public static void EnsureCanClose(FundStatus status, int unsettledEventCount)
    {
        if (status != FundStatus.Active)
        {
            throw new BusinessRuleException(AlreadyClosedCode, "Quỹ đã tổng kết cuối năm.");
        }

        if (unsettledEventCount > 0)
        {
            throw new BusinessRuleException(OpenEventsCode, "Còn sự kiện chưa quyết toán hoặc chưa hủy.");
        }
    }

    /// <summary>Quỹ đã tổng kết thì không nhận khoản thu, giao dịch, sự kiện mới.</summary>
    public static void EnsureFundActive(FundStatus status)
    {
        if (status != FundStatus.Active)
        {
            throw new BusinessRuleException(FundArchivedCode, "Quỹ đã tổng kết cuối năm, chỉ được xem.");
        }
    }
}

/// <summary>Chuyển trạng thái sự kiện hợp lệ (xem docs/business-analysis/business-rules-and-processes.md).</summary>
public static class EventStatusRules
{
    public const string InvalidTransitionCode = "event.invalid_transition";

    private static readonly IReadOnlyDictionary<EventStatus, EventStatus[]> Allowed = new Dictionary<EventStatus, EventStatus[]>
    {
        [EventStatus.Open] = [EventStatus.ListLocked, EventStatus.Cancelled],
        [EventStatus.ListLocked] = [EventStatus.Completed],
        [EventStatus.Completed] = [EventStatus.Settled],
        [EventStatus.Settled] = [],
        [EventStatus.Cancelled] = [],
    };

    public static bool CanTransition(EventStatus from, EventStatus to) => Allowed[from].Contains(to);

    public static void EnsureCanTransition(EventStatus from, EventStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new BusinessRuleException(InvalidTransitionCode, "Không thể chuyển trạng thái sự kiện theo cách này.");
        }
    }

    /// <summary>Còn nhận khoản thu/chi mới? Sau khi quyết toán hoặc hủy thì không.</summary>
    public static bool AcceptsNewMoney(EventStatus status) =>
        status is EventStatus.Open or EventStatus.ListLocked or EventStatus.Completed;
}

/// <summary>Kết quả xác định cách ghi nhận một lần xác nhận thanh toán.</summary>
public readonly record struct ConfirmationDecision(bool Allowed, ConfirmationMode Mode);

/// <summary>
/// Quy tắc xác nhận đóng tiền (yêu cầu 1.3, 3.3): người xác nhận phải có Payment.Confirm;
/// người nộp == người xác nhận ⇒ thủ quỹ tự ghi nhận (SelfDeclared), ngược lại ⇒ Verified.
/// </summary>
public static class PaymentConfirmationPolicy
{
    public static ConfirmationDecision Decide(Guid actorUserId, Guid payerUserId, bool actorCanConfirm)
    {
        if (!actorCanConfirm)
        {
            return new ConfirmationDecision(false, ConfirmationMode.Verified);
        }

        return new ConfirmationDecision(true, actorUserId == payerUserId ? ConfirmationMode.SelfDeclared : ConfirmationMode.Verified);
    }
}

/// <summary>Danh mục mặc định tạo cùng quỹ (seed; chạy lại không tạo trùng vì khóa theo tên + loại).</summary>
public static class DefaultCategories
{
    public static readonly IReadOnlyList<(string Name, LedgerType Type)> All =
    [
        ("Đóng góp quỹ lớp", LedgerType.Income),
        ("Thu bổ sung", LedgerType.Income),
        ("Thu khác", LedgerType.Income),
        ("In ấn", LedgerType.Expense),
        ("Văn phòng phẩm", LedgerType.Expense),
        ("Bánh kẹo, tiếp khách", LedgerType.Expense),
        ("Đồ dùng chung", LedgerType.Expense),
        ("Chi khác", LedgerType.Expense),
    ];
}
