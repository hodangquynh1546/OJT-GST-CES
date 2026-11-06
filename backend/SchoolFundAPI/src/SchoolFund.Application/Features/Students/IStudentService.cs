using System.Text.Json.Serialization;

namespace SchoolFund.Application.Features.Students;

/// <summary>Trạng thái tài khoản học sinh trên màn hình G-02.</summary>
public enum StudentAccountStatus
{
    /// <summary>Chưa cấp tài khoản.</summary>
    None,

    /// <summary>Đã cấp nhưng chưa đổi mật khẩu tạm – chưa phân công thủ quỹ được (BR-31).</summary>
    PendingActivation,

    Active,
    Disabled,
}

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

/// <summary>Học sinh và phụ huynh của một quỹ – chỉ GVCN (<c>Student.Manage</c>). FR-24, FR-25, FR-26.</summary>
public interface IStudentService
{
    Task<IReadOnlyList<StudentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">Học sinh không thuộc quỹ.</exception>
    Task<StudentResponse> GetAsync(Guid fundId, Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm học sinh (chưa có tài khoản); có email phụ huynh thì liên kết luôn trong cùng thao tác (P-13).</summary>
    Task<StudentCreatedResponse> CreateAsync(
        Guid fundId,
        string fullName,
        string? parentEmail,
        string? parentFullName,
        CancellationToken cancellationToken);

    Task RenameAsync(Guid fundId, Guid id, string fullName, CancellationToken cancellationToken);

    /// <summary>Cấp tài khoản: tên đăng nhập (bỏ trống → gợi ý) + mật khẩu tạm một lần (P-14, BR-29).</summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>student.account_exists</c>, <c>user.username</c>, <c>user.username_taken</c>.</exception>
    Task<CredentialResponse> IssueAccountAsync(Guid fundId, Guid id, string? username, CancellationToken cancellationToken);

    /// <summary>Cấp lại mật khẩu tạm; mọi phiên cũ hết hiệu lực (P-16, BR-35).</summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>student.no_account</c>.</exception>
    Task<CredentialResponse> ResetPasswordAsync(Guid fundId, Guid id, CancellationToken cancellationToken);

    /// <summary>Liên kết phụ huynh theo email (BR-30).</summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>parent_link.exists</c>, <c>parent.email_in_use</c>.</exception>
    Task<ParentLinkResponse> LinkParentAsync(
        Guid fundId,
        Guid id,
        string email,
        string? fullName,
        CancellationToken cancellationToken);

    /// <summary>Gỡ liên kết = đánh dấu, giữ lịch sử (BR-33).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Học sinh chưa có liên kết phụ huynh.</exception>
    Task UnlinkParentAsync(Guid fundId, Guid id, CancellationToken cancellationToken);
}
