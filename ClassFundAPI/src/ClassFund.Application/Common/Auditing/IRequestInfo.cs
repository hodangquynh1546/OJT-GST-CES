using System.Net;

namespace ClassFund.Application.Common.Auditing;

/// <summary>Thông tin request ghi kèm nhật ký.</summary>
public interface IRequestInfo
{
    string? CorrelationId { get; }

    IPAddress? IpAddress { get; }
}
