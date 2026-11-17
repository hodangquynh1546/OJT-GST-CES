namespace ClassFund.Api.Hosting;

public static class RateLimitPolicies
{
    /// <summary>Đăng nhập, đăng ký: giới hạn theo IP trong cửa sổ 1 phút (TD-68).</summary>
    public const string Auth = "auth";

    public const string AuthPermitLimitSetting = "RateLimiting:AuthPermitLimit";
    public const int DefaultAuthPermitLimit = 10;
    public const string RejectedCode = "auth.rate_limited";
}
