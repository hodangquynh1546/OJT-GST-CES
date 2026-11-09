using FluentValidation;

namespace ClassFund.Api.Common;

/// <summary>Tham số phân trang chung (<c>?page=&amp;pageSize=</c>); request danh sách kế thừa record này.</summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Trang, bắt đầu từ 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Số dòng mỗi trang, 1–100, mặc định 20.</summary>
    public int PageSize { get; init; } = DefaultPageSize;
}

public static class PageRequestRules
{
    /// <summary>Gọi trong constructor validator của request kế thừa <see cref="PageRequest"/>: <c>this.ValidatePaging();</c>.</summary>
    public static void ValidatePaging<T>(this AbstractValidator<T> validator)
        where T : PageRequest
    {
        validator.RuleFor(request => request.Page).GreaterThanOrEqualTo(1).WithMessage("Trang phải từ 1.");
        validator.RuleFor(request => request.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang từ 1 đến {PageRequest.MaxPageSize}.");
    }
}

/// <summary>Validator mặc định khi request dùng thẳng <see cref="PageRequest"/>.</summary>
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator() => this.ValidatePaging();
}
