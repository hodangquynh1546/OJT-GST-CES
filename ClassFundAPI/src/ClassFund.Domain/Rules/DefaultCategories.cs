using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Rules;

/// <summary>Danh mục tạo sẵn cho mỗi quỹ mới (D-25, Q-09). Thủ quỹ đổi tên / ẩn / thêm sau.</summary>
public static class DefaultCategories
{
    public static readonly IReadOnlyList<(TransactionType Type, string Name)> All =
    [
        (TransactionType.Income, "Đóng quỹ"),
        (TransactionType.Income, "Thu khác"),
        (TransactionType.Expense, "Văn phòng phẩm"),
        (TransactionType.Expense, "Sinh hoạt, liên hoan"),
        (TransactionType.Expense, "Khen thưởng"),
        (TransactionType.Expense, "Chi khác"),
    ];
}
