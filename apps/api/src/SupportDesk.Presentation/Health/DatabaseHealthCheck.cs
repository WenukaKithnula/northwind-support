using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Healthy when the API can open a connection to its database. It asks only that: no query runs,
/// so the check stays cheap enough to be called every few seconds by a load balancer.
/// </summary>
public sealed class DatabaseHealthCheck(SupportDbContext db) : IHealthCheck
{
    public const string Name = "database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // CanConnectAsync reports an unreachable database as false; it does not throw for that.
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database cannot be reached.");
    }
}
