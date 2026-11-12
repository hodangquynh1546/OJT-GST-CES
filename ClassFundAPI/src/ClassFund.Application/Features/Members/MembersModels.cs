using ClassFund.Domain.Authorization;

namespace ClassFund.Application.Features.Members;

/// <summary>Chỉ họ tên và vai trò – không email, tên đăng nhập, trạng thái tài khoản (BR-23).</summary>
public sealed record MemberResponse(string FullName, FundRole Role);
