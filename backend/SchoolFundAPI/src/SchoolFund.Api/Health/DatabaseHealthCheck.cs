using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolFund.Application.Common.Persistence;

namespace SchoolFund.Api.Health;

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
