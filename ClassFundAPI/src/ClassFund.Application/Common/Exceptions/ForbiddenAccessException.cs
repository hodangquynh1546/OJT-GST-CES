namespace ClassFund.Application.Common.Exceptions;

/// <summary>Bị từ chối; API trả 403 kèm <see cref="Code"/> (<c>forbidden</c>, <c>password.change_required</c>).</summary>
public sealed class ForbiddenAccessException(
    string code = ForbiddenAccessException.DefaultCode,
    string message = "Bạn không có quyền thực hiện thao tác này.") : Exception(message)
{
    public const string DefaultCode = "forbidden";
    public const string PasswordChangeRequiredCode = "password.change_required";

    public string Code { get; } = code;
}
