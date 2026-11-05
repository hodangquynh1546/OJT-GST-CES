namespace SchoolFund.Application.Common.Security;

/// <summary>Trạng thái phiên còn hiệu lực của một tài khoản.</summary>
public sealed record ActiveSession(Guid UserId, bool MustChangePassword);

/// <summary>Kiểm tra phiên ở mỗi request: tài khoản còn hoạt động và SecurityStamp còn khớp.</summary>
public interface ISessionStore
{
    /// <returns><c>null</c> khi phiên không còn hiệu lực.</returns>
    Task<ActiveSession?> FindActiveAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
}
