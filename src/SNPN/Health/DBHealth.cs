using Dapper;

namespace SNPN.Health;

public interface IDBHealth
{
	public Task<bool> CheckHealth();
}

public class DBHealth : DbHelper, IDBHealth
{
	private readonly ILogger<DBHealth> _logger;

	public DBHealth(ILogger<DBHealth> logger, AppConfiguration configuration) : base(logger, configuration) => _logger = logger;
	public async Task<bool> CheckHealth()
	{
		try
		{
			using var conn = GetConnection();
			await conn.ExecuteAsync("SELECT 1");
		}
		catch(Exception ex)
		{
			_logger.LogError(ex, "Error checking DB health.");
			return false;
		}
		return true;
	}
}