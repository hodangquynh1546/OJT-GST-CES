using ClassFund.Domain.Exceptions;

namespace ClassFund.Domain.Rules;

/// <summary>Số tiền một người đã đóng (chỉ để hiển thị; không ảnh hưởng cách chia).</summary>
public readonly record struct Contribution(Guid UserId, long Amount);

/// <summary>Phần tiền thừa của một người.</summary>
public readonly record struct Share(Guid UserId, long Contributed, long Amount);

/// <summary>
/// Chia tiền thừa ĐỀU theo đầu người (mentor, 2026-11): mọi người còn lại đều đã đóng đủ nên chia đều, không chia theo tỷ lệ.
/// Mỗi phần là bội của 1.000đ để số dư rút được (rút theo bội 1.000); số lẻ dưới 1.000đ không chia được ở lại sự kiện / quỹ.
/// Dùng chung cho quyết toán sự kiện và tổng kết cuối năm; thuần, không phụ thuộc hạ tầng.
/// </summary>
public static class SettlementCalculator
{
    public const long Unit = WithdrawalRule.Step;
    public const string NoRecipientsCode = "settlement.no_recipients";

    public static IReadOnlyList<Share> AllocateEqually(long surplus, IReadOnlyCollection<Contribution> people)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(surplus);

        if (surplus == 0 || people.Count == 0)
        {
            if (surplus > 0)
            {
                throw new BusinessRuleException(NoRecipientsCode, "Không có người nhận nên không chia được tiền thừa.");
            }

            return [.. people.Select(item => new Share(item.UserId, item.Amount, 0))];
        }

        var units = surplus / Unit;
        var each = units / people.Count;
        var extra = (int)(units % people.Count);

        // Phần lẻ (đơn vị 1.000đ) phát lần lượt theo mã người dùng để kết quả luôn giống nhau.
        var lucky = people.OrderBy(item => item.UserId).Take(extra).Select(item => item.UserId).ToHashSet();
        return [.. people.Select(item => new Share(item.UserId, item.Amount, (each + (lucky.Contains(item.UserId) ? 1 : 0)) * Unit))];
    }
}
