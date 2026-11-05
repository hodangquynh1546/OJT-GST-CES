using SchoolFund.Application.Common.Security;

namespace SchoolFund.Infrastructure.Security;

/// <summary>
/// Cài đặt tạm khi chưa có bảng User: từ chối mọi phiên và mọi quyền (fail closed).
/// Feature Auth thay lớp này bằng SessionStore và AccessService đọc dữ liệu thật.
/// </summary>
public sealed class NoIdentityStore : ISessionStore, IAccessService
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public Task<ActiveSession?> FindActiveAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        Task.FromResult<ActiveSession?>(null);

    public Task<IReadOnlySet<string>> GetAccountPermissionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(None);

    public Task<FundAccess?> GetFundAccessAsync(Guid fundId, CancellationToken cancellationToken) =>
        Task.FromResult<FundAccess?>(null);
}
