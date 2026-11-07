using Microsoft.Extensions.Diagnostics.HealthChecks;
using ClassFund.Application.Common.Persistence;

namespace ClassFund.Api.Health;

public sealed class DatabaseHealthCheck(IDatabaseProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await probe.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Không kết nối được CSDL.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Không kết nối được CSDL.", exception);
        }
    }
}
