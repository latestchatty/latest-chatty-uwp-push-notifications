using Serilog;
using SNPN.Common;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Monitor
{
	class Monitor : IDisposable
	{
		const int BASE_TIME_DELAY = 2;
		const double TIME_DELAY_FAIL_EXPONENT = 1.5;
		Timer mainTimer;
		double timeDelay = 0;
		bool timerEnabled = false;
		long lastEventId = 0;
		private readonly INotificationService notificationService;
		private readonly AppConfiguration configuration;
		private readonly ILogger logger;
		private readonly Func<NewEventHandler> createHandlerFunc;
		private readonly INetworkService networkService;
		private CancellationTokenSource cancelToken = new CancellationTokenSource();

		public Monitor(INotificationService notificationService, AppConfiguration config, ILogger logger, Func<NewEventHandler> createHandlerFunc, INetworkService networkService)
		{
			this.notificationService = notificationService;
			this.configuration = config;
			this.logger = logger;
			this.createHandlerFunc = createHandlerFunc;
			this.networkService = networkService;
		}

		public void Start()
		{
			if (this.timerEnabled) return;
			this.timerEnabled = true;
			this.mainTimer = new System.Threading.Timer(TimerCallback, null, 0, System.Threading.Timeout.Infinite);
			this.logger.Verbose("Notification monitor started.");
		}

		public void Stop()
		{
			this.timerEnabled = false;
			this.cancelToken.Cancel();
			if (this.mainTimer != null)
			{
				this.mainTimer.Dispose();
				this.mainTimer = null;
			}
			this.logger.Verbose("Notification monitor stopped.");
		}

		async private void TimerCallback(object state)
		{
			var parser = new EventParser();
			this.logger.Verbose("Waiting for next monitor event...");
			try
			{
				if (this.lastEventId == 0)
				{
					this.lastEventId = await this.networkService.WinChattyGetNewestEventId(this.cancelToken.Token);
				}

				var jEvents = await this.networkService.WinChattyWaitForEvent(this.lastEventId, this.cancelToken.Token);

				var newPostEvents = parser.GetNewPostEvents(jEvents);
				foreach (var newPost in newPostEvents)
				{
					var handler = this.createHandlerFunc();
					await handler.ProcessEvent(newPost);
				}

				this.lastEventId = parser.GetLatestEventId(jEvents);

				timeDelay = 0;
			}
			catch (Exception ex)
			{
				if (timeDelay == 0)
				{
					timeDelay = BASE_TIME_DELAY;
				}
				//There was a problem, delay further.  To a maximum of 10 minutes.
				timeDelay = Math.Min(Math.Pow(timeDelay, TIME_DELAY_FAIL_EXPONENT), 600);
				if (ex is TaskCanceledException)
				{
					//This is expected, we'll still slow down our polling of winchatty if the chatty's not busy but won't print a full stack.
					//Don't reset the event ID though, since nothing happened.  Don't want to miss events.
					this.logger.Verbose("Timed out waiting for winchatty.");
				}
				else
				{
					//If there was an error, reset the event ID to 0 so we get the latest, otherwise we might get stuck in a loop where the API won't return us events because there are too many.
					lastEventId = 0;
					this.logger.Error(ex, $"Exception in {nameof(TimerCallback)}");
				}
			}
			finally
			{
				if (this.timerEnabled)
				{
					this.logger.Verbose("Delaying next monitor for {monitorDelay}ms", this.timeDelay * 1000);
					mainTimer.Change((int)(this.timeDelay * 1000), Timeout.Infinite);
				}
			}
		}



		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (this.mainTimer != null)
					{
						this.mainTimer.Dispose();
					}
					if (this.cancelToken != null)
					{
						this.cancelToken.Dispose();
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~Monitor() {
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
