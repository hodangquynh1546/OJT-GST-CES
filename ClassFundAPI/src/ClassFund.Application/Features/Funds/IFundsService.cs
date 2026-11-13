namespace ClassFund.Application.Features.Funds;

public interface IFundsService
{
    /// <summary>Mọi quỹ người dùng hiện tại có vai trò (GVCN, thủ quỹ, học sinh, phụ huynh), sắp theo tên (FR-22).</summary>
    Task<IReadOnlyList<FundSummaryResponse>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>Tổng quan một quỹ cho S-01: số dư, tổng thu / chi, tháng hiện tại, người quản lý, 5 khoản gần nhất.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Quỹ không tồn tại hoặc người dùng không thuộc quỹ.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có quyền <c>Fund.View</c>.</exception>
    Task<FundOverviewResponse> GetAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Giáo viên tạo quỹ: người tạo là GVCN, kèm danh mục mặc định, chưa có thủ quỹ (FR-23, Q-09).</summary>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có quyền <c>Fund.Create</c>.</exception>
    Task<FundCreatedResponse> CreateAsync(string name, CancellationToken cancellationToken);

    /// <summary>GVCN đổi tên quỹ (FR-11).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Quỹ không tồn tại hoặc không thuộc quỹ.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenAccessException">Không có quyền <c>Fund.Manage</c>.</exception>
    Task RenameAsync(Guid fundId, string name, CancellationToken cancellationToken);
}
