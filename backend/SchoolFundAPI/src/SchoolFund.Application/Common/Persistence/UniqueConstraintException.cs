namespace SchoolFund.Application.Common.Persistence;

/// <summary>Database từ chối ghi vì trùng unique; service đổi thành lỗi nghiệp vụ khi hai request cùng ghi một lúc.</summary>
public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception("Dữ liệu vi phạm ràng buộc duy nhất.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
