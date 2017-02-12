using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Common
{
	public class AccessTokenManager : IDisposable
	{
		private string accessToken = string.Empty;
		private readonly ILogger logger;
		private readonly INetworkService networkService;
		SemaphoreSlim locker = new SemaphoreSlim(1);

		AccessTokenManager(ILogger logger, INetworkService networkService)
		{
			this.logger = logger;
			this.networkService = networkService;
		}
	
		public async Task<string> GetAccessToken()
		{
			try
			{
				//Make sure we don't try to get the token multiple times in a row
				await this.locker.WaitAsync();
				if (string.IsNullOrWhiteSpace(this.accessToken))
				{
					this.logger.Information("Getting access token.");
					this.accessToken = await this.networkService.GetNotificationToken();
				}
			}
			finally
			{
				this.locker.Release();
			}
			return this.accessToken;
		}

		public async Task<string> RefreshAccessToken()
		{
			this.InvalidateToken();
			return await this.GetAccessToken();
		}

		public void InvalidateToken()
		{
			this.accessToken = string.Empty;
		}

		#region IDisposable Support
		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.locker.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
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
}
