namespace ClassFund.Application.Common.Persistence;

/// <summary>Kiểm tra kết nối CSDL cho health check.</summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
