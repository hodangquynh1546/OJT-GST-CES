namespace SchoolFund.Application.Common.Paging;

/// <summary>Một trang kết quả; <see cref="Total"/> đếm sau khi đã lọc theo phạm vi người xem.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
