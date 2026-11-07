namespace ClassFund.Domain.Exceptions;

/// <summary>Vi phạm quy tắc nghiệp vụ; API trả 422 kèm <see cref="Code"/> (ví dụ <c>treasurer.last</c>).</summary>
public sealed class BusinessRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
