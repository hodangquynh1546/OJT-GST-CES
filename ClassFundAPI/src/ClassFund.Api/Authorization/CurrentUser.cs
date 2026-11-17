using ClassFund.Application.Common.Security;

namespace ClassFund.Api.Authorization;

/// <summary>Cài đặt <see cref="ICurrentUser"/>, do CurrentUserMiddleware điền một lần cho mỗi request.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private ActiveSession? _session;

    public bool IsAuthenticated => _session is not null;

    public Guid? UserId => _session?.UserId;

    public bool MustChangePassword => _session?.MustChangePassword ?? false;

    public void Set(ActiveSession session) => _session = session;
}
