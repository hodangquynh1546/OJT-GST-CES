namespace SchoolFund.Domain.Rules;

/// <summary>Ngày nghiệp vụ theo giờ Việt Nam (UTC+7, không có giờ mùa hè).</summary>
public static class VietnamTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly DateOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToOffset(Offset).DateTime);
}
