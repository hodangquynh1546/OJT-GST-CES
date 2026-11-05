namespace SchoolFund.Application.Common.Exceptions;

/// <summary>Xung đột trạng thái; API trả 409 kèm <see cref="Code"/> (ví dụ <c>treasurer.already_revoked</c>).</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
