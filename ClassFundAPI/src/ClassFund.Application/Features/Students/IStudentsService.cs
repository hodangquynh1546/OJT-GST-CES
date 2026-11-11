namespace ClassFund.Application.Features.Students;

/// <summary>Học sinh và phụ huynh của một quỹ – chỉ GVCN (<c>Student.Manage</c>). FR-24, FR-25, FR-26.</summary>
public interface IStudentsService
{
    Task<IReadOnlyList<StudentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">Học sinh không thuộc quỹ.</exception>
    Task<StudentResponse> GetAsync(Guid fundId, Guid id, CancellationToken cancellationToken);

    /// <summary>Thêm học sinh (chưa có tài khoản); có email phụ huynh thì liên kết luôn trong cùng thao tác.</summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>parent.email_in_use</c>.</exception>
    Task<StudentCreatedResponse> CreateAsync(
        Guid fundId,
        string fullName,
        string? parentEmail,
        string? parentFullName,
        CancellationToken cancellationToken);

    Task RenameAsync(Guid fundId, Guid id, string fullName, CancellationToken cancellationToken);

    /// <summary>Cấp tài khoản: tên đăng nhập (bỏ trống → gợi ý) + mật khẩu tạm một lần (BR-29).</summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException"><c>student.account_exists</c>, <c>user.username</c>, <c>user.username_taken</c>.</exception>
    Task<CredentialResponse> IssueAccountAsync(Guid fundId, Guid id, string? username, CancellationToken cancellationToken);

    /// <summary>Cấp lại mật khẩu tạm; mọi phiên cũ hết hiệu lực (BR-35).</summary>
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
