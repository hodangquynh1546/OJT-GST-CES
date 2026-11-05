namespace SchoolFund.Api.Authorization;

/// <summary>Cookie phiên chỉ chứa mã người dùng và SecurityStamp.</summary>
public static class SessionClaims
{
    public const string UserId = "sub";
    public const string SecurityStamp = "stamp";
}
