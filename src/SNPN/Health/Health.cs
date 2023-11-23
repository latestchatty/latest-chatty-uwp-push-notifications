using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SNPN.Health;

public class Health : IHealthCheck
{
	private IDBHealth _dbHealth;
	public Health(IDBHealth dBHealth) => _dbHealth = dBHealth;

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		if(!await _dbHealth.CheckHealth()) return HealthCheckResult.Unhealthy("Can't connect to database.");
		return HealthCheckResult.Healthy();
	}
}
