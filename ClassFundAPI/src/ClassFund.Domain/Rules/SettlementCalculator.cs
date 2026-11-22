using ClassFund.Domain.Exceptions;

namespace ClassFund.Domain.Rules;

/// <summary>Số tiền một người đã đóng (đầu vào chia tiền thừa).</summary>
public readonly record struct Contribution(Guid UserId, long Amount);

/// <summary>Phần tiền thừa của một người.</summary>
public readonly record struct Share(Guid UserId, long Contributed, long Amount);

/// <summary>
/// Chia tiền thừa theo TỶ LỆ số tiền mỗi người đã đóng (OQ-18, BR-22). Thuần, không phụ thuộc hạ tầng; dùng chung với tổng kết cuối năm.
/// Phương pháp phần dư lớn nhất: mỗi người nhận phần nguyên, số đồng còn lại phát lần lượt cho phần dư lớn nhất → tổng chia đúng bằng số thừa.
/// </summary>
public static class SettlementCalculator
{
    public const string NoContributorsCode = "settlement.no_contributors";

    public static IReadOnlyList<Share> Allocate(long surplus, IReadOnlyCollection<Contribution> contributions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(surplus);

        var total = contributions.Sum(item => item.Amount);
        if (surplus == 0)
        {
            return [.. contributions.Select(item => new Share(item.UserId, item.Amount, 0))];
        }

        if (total <= 0)
        {
            throw new BusinessRuleException(NoContributorsCode, "Không có ai đã đóng tiền nên không chia được tiền thừa.");
        }

        var rows = contributions
            .Select(item =>
            {
                var numerator = (Int128)surplus * item.Amount;
                return (item.UserId, item.Amount, Floor: (long)(numerator / total), Remainder: (long)(numerator % total));
            })
            .ToArray();

        var leftover = surplus - rows.Sum(row => row.Floor);
        var winners = rows
            .OrderByDescending(row => row.Remainder)
            .ThenByDescending(row => row.Amount)
            .ThenBy(row => row.UserId)
            .Take((int)leftover)
            .Select(row => row.UserId)
            .ToHashSet();

        return [.. rows.Select(row => new Share(row.UserId, row.Amount, row.Floor + (winners.Contains(row.UserId) ? 1 : 0)))];
    }
}
