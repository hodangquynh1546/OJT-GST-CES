namespace ClassFund.Application.Features.Ledger;

public static class LedgerErrors
{
    /// <summary>BR-09: ngày giao dịch không ở tương lai theo giờ Việt Nam.</summary>
    public const string FutureDate = "ledger.future_date";
    public const string FutureDateMessage = "Ngày giao dịch không được ở tương lai.";

    /// <summary>BR-20: sự kiện đã kết thúc / quyết toán / hủy thì không ghi chi mới.</summary>
    public const string EventClosed = "ledger.event_closed";
    public const string EventClosedMessage = "Sự kiện đã kết thúc hoặc đã quyết toán / hủy, không ghi chi mới.";

    /// <summary>Sự kiện đã quyết toán / hủy: sổ của sự kiện đã khóa, không đảo được nữa.</summary>
    public const string ScopeLocked = "ledger.scope_locked";
    public const string ScopeLockedMessage = "Sự kiện đã quyết toán hoặc đã hủy, sổ thu chi của sự kiện không thay đổi được nữa.";

    /// <summary>BR-08: một dòng chỉ bị đảo một lần.</summary>
    public const string AlreadyReversed = "ledger.already_reversed";
    public const string AlreadyReversedMessage = "Giao dịch này đã được đảo.";

    /// <summary>
    /// Chỉ đảo khoản chi ghi tay. Thu từ thanh toán đã xác nhận sửa ở luồng thanh toán (M6);
    /// hoàn tiền / rút số dư sửa ở luồng của chúng; bút toán đảo không đảo tiếp.
    /// </summary>
    public const string NotReversible = "ledger.not_reversible";
    public const string NotReversibleMessage = "Chỉ đảo được khoản chi ghi tay chưa bị đảo.";

    public const string ReasonRequired = "ledger.reason_required";
    public const string ReasonRequiredMessage = "Vui lòng nhập lý do đảo.";
}
