namespace SchoolFund.Domain.Authorization;

/// <summary>Vai trò trong một quỹ. Không lưu trong DB: backend suy ra từ dữ liệu ở mỗi request.</summary>
public enum FundRole
{
    Teacher = 1,
    Treasurer = 2,
    Student = 3,
    Parent = 4,
}
