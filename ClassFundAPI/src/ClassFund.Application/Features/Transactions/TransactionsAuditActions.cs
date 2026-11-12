namespace ClassFund.Application.Features.Transactions;

/// <summary>Tên hành động AuditLog của module Thu chi (BR-11; api-conventions.md mục 8). Lịch sử thay đổi đọc theo các tên này.</summary>
public static class TransactionsAuditActions
{
    public const string Create = "Transaction.Create";
    public const string Update = "Transaction.Update";
    public const string Cancel = "Transaction.Cancel";
}
