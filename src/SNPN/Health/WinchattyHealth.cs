using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SNPN;

public class WinchattyHealth : IHealthCheck
{
	private readonly INetworkService _networkService;
	private readonly ILogger<WinchattyHealth> _logger;

	public WinchattyHealth(INetworkService networkService, ILogger<WinchattyHealth> logger)
	{
		_networkService = networkService;
		_logger = logger;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var eventId = await _networkService.WinChattyGetNewestEventId(cancellationToken);
			return HealthCheckResult.Healthy();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error calling winchatty.");
		}
		return HealthCheckResult.Unhealthy("Winchatty is unreachable.");
	}
}
