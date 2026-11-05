using SchoolFund.Application.Common.Security;

namespace SchoolFund.Infrastructure.Security;

/// <summary>
/// Cài đặt tạm của <see cref="ISessionStore"/>: từ chối mọi phiên (fail closed).
/// Feature Auth thay lớp này bằng SessionStore đọc bảng User.
/// </summary>
public sealed class NoIdentityStore : ISessionStore
{
    public Task<ActiveSession?> FindActiveAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        Task.FromResult<ActiveSession?>(null);
}
