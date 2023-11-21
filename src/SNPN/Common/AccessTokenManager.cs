namespace SNPN.Common;

public class AccessTokenManager : IDisposable
{
	private string _accessToken = string.Empty;
	private readonly ILogger<AccessTokenManager> _logger;
	private readonly INetworkService _networkService;
	private readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

	public AccessTokenManager(ILogger<AccessTokenManager> logger, INetworkService networkService)
	{
		_logger = logger;
		_networkService = networkService;
	}

	public async Task<string> GetAccessToken()
	{
		try
		{
			//Make sure we don't try to get the token multiple times in a row
			await _locker.WaitAsync();
			if (string.IsNullOrWhiteSpace(_accessToken))
			{
				_logger.LogInformation("Getting access token.");
				_accessToken = await _networkService.GetNotificationToken();
			}
		}
		finally
		{
			_locker.Release();
		}
		return _accessToken;
	}

	public async Task<string> RefreshAccessToken()
	{
		InvalidateToken();
		return await GetAccessToken();
	}

	public void InvalidateToken()
	{
		_accessToken = string.Empty;
	}

	#region IDisposable Support
	private bool _disposedValue; // To detect redundant calls

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_locker.Dispose();
			}

			// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
			// TODO: set large fields to null.

			_disposedValue = true;
		}
	}

	// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
	// ~AccessTokenManager() {
	//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
	//   Dispose(false);
	// }

	// This code added to correctly implement the disposable pattern.
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		Dispose(true);
		// TODO: uncomment the following line if the finalizer is overridden above.
		// GC.SuppressFinalize(this);
	}
	#endregion
}
