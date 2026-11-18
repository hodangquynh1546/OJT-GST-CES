namespace ClassFund.Api.Hosting;

public static class RateLimitPolicies
{
    /// <summary>Đăng nhập, đăng ký: giới hạn theo IP trong cửa sổ 1 phút (TD-68).</summary>
    public const string Auth = "auth";

    /// <summary>Thêm nhiều email một lần (OQ-20): chặt hơn đăng nhập vì mỗi lần tạo tới 50 tài khoản (băm mật khẩu tốn CPU); theo người dùng.</summary>
    public const string MembersBulk = "members-bulk";

    public const int MembersBulkPermitLimit = 10;

    public const string AuthPermitLimitSetting = "RateLimiting:AuthPermitLimit";
    public const int DefaultAuthPermitLimit = 10;
    public const string RejectedCode = "auth.rate_limited";
}
