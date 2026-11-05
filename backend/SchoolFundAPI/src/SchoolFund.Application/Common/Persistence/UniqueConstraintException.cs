namespace SchoolFund.Application.Common.Persistence;

/// <summary>Ghi dữ liệu vi phạm ràng buộc UNIQUE (ví dụ hai request đăng ký cùng email đồng thời).</summary>
public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception("Dữ liệu bị trùng.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
