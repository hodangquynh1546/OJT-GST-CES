using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Ledger;

/// <summary>
/// Tổng thu / chi / số dư của một phạm vi tính từ sổ (dùng cho M7, M8 và sau này M9, M13).
/// Bút toán đảo của khoản chi là một dòng thu (và ngược lại): trừ nó khỏi tổng của loại dòng gốc
/// để "tổng chi" không tính khoản đã đảo (BR-21) mà số dư vẫn đúng bằng thu − chi.
/// </summary>
public static class LedgerBalances
{
    /// <param name="eventId">null = quỹ chung; có giá trị = sự kiện.</param>
    public static async Task<ScopeTotals> ForScopeAsync(
        IAppDb db,
        Guid fundId,
        Guid? eventId,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var entries = InScope(db, fundId, eventId);
        if (from is { } start)
        {
            entries = entries.Where(entry => entry.TransactionDate >= start);
        }

        if (to is { } end)
        {
            entries = entries.Where(entry => entry.TransactionDate <= end);
        }

        var rows = await db.ToListAsync(
            entries
                .GroupBy(entry => new { entry.Type, IsReversal = entry.ReversesEntryId != null })
                .Select(group => new SumRow(group.Key.Type, group.Key.IsReversal, group.Sum(entry => entry.Amount))),
            cancellationToken);

        return Combine(rows);
    }

    /// <summary>Tổng của nhiều sự kiện trong một truy vấn (E-01).</summary>
    public static async Task<IReadOnlyDictionary<Guid, ScopeTotals>> ForEventsAsync(
        IAppDb db,
        Guid fundId,
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
        {
            return new Dictionary<Guid, ScopeTotals>();
        }

        var ids = eventIds.ToArray();
        var rows = await db.ToListAsync(
            db.Query<LedgerEntry>()
                .Where(entry => entry.FundId == fundId && entry.EventId != null && ids.Contains(entry.EventId.Value))
                .GroupBy(entry => new { EventId = entry.EventId!.Value, entry.Type, IsReversal = entry.ReversesEntryId != null })
                .Select(group => new
                {
                    group.Key.EventId,
                    Row = new SumRow(group.Key.Type, group.Key.IsReversal, group.Sum(entry => entry.Amount)),
                }),
            cancellationToken);

        return rows
            .GroupBy(row => row.EventId)
            .ToDictionary(group => group.Key, group => Combine(group.Select(row => row.Row)));
    }

    /// <summary>Các dòng của đúng một phạm vi (BR-01): quỹ chung không gồm giao dịch sự kiện.</summary>
    public static IQueryable<LedgerEntry> InScope(IAppDb db, Guid fundId, Guid? eventId)
    {
        var entries = db.Query<LedgerEntry>().Where(entry => entry.FundId == fundId);
        return eventId is { } id
            ? entries.Where(entry => entry.EventId == id)
            : entries.Where(entry => entry.EventId == null);
    }

    private static ScopeTotals Combine(IEnumerable<SumRow> rows)
    {
        long income = 0, expense = 0;
        foreach (var row in rows)
        {
            switch (row.Type, row.IsReversal)
            {
                case (LedgerType.Income, false):
                    income += row.Sum;
                    break;
                case (LedgerType.Expense, false):
                    expense += row.Sum;
                    break;
                case (LedgerType.Income, true): // đảo một khoản chi
                    expense -= row.Sum;
                    break;
                case (LedgerType.Expense, true): // đảo một khoản thu
                    income -= row.Sum;
                    break;
            }
        }

        return new ScopeTotals(income, expense);
    }

    private sealed record SumRow(LedgerType Type, bool IsReversal, long Sum);
}
