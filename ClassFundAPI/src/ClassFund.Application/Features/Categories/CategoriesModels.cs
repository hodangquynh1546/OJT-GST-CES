using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Categories;

public sealed record CreateCategoryInput(string Name, LedgerType Type);

/// <summary>Đổi tên và/hoặc ngừng dùng – dùng lại (FR-14). Danh mục không bao giờ bị xóa.</summary>
public sealed record UpdateCategoryInput(string Name, bool IsActive);

/// <summary>Danh mục kèm số giao dịch đã dùng (C-01 hiện "Mặc định · n giao dịch").</summary>
public sealed record CategoryDto(Guid Id, string Name, LedgerType Type, bool IsSystem, bool IsActive, int UsageCount);
