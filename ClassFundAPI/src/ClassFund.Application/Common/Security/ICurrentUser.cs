namespace ClassFund.Application.Common.Security;

/// <summary>Người dùng của request hiện tại, chỉ lấy từ phiên đăng nhập, không bao giờ từ dữ liệu client gửi lên.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    bool MustChangePassword { get; }
}
