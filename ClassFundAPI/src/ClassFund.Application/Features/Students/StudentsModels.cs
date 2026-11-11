using System.Text.Json.Serialization;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Students;

/// <summary>Phụ huynh đang liên kết; <c>pendingActivation</c> = chưa đổi mật khẩu tạm.</summary>
public sealed record StudentParentResponse(string FullName, string? Email, bool PendingActivation);

public sealed record StudentResponse(
    Guid Id,
    string FullName,
    string? Username,
    StudentAccountStatus AccountStatus,
    bool IsTreasurer,
    StudentParentResponse? Parent,
    DateTimeOffset CreatedAt);

/// <summary>Thông tin đăng nhập chỉ hiển thị một lần; response chứa nó có <c>Cache-Control: no-store</c> (NFR-11).</summary>
public sealed record CredentialResponse(string Login, string TemporaryPassword);

/// <summary>Kết quả liên kết phụ huynh. Hệ thống không gửi email (D-33) nên <c>emailSent</c> luôn <c>false</c>.</summary>
public sealed record ParentLinkResponse(bool AccountCreated, CredentialResponse? Credential, bool EmailSent);

public sealed record StudentCreatedResponse(
    Guid Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ParentLinkResponse? Parent);
