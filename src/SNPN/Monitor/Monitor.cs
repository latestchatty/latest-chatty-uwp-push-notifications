using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SNPN.Common;

namespace SNPN.Monitor
{
	class Monitor : IDisposable
	{
		private const int BaseTimeDelay = 2;
		private const double TimeDelayFailExponent = 1.5;
		Timer _mainTimer;
		double _timeDelay;
		bool _timerEnabled;
		long _lastEventId;
		private readonly ILogger _logger;
		private readonly Func<NewEventHandler> _createHandlerFunc;
		private readonly INetworkService _networkService;
		private readonly CancellationTokenSource _cancelToken = new CancellationTokenSource();

		public Monitor(ILogger logger, Func<NewEventHandler> createHandlerFunc, INetworkService networkService)
		{
			_logger = logger;
			_createHandlerFunc = createHandlerFunc;
			_networkService = networkService;
		}

		public void Start()
		{
			if (_timerEnabled) return;
			_timerEnabled = true;
			_mainTimer = new Timer(TimerCallback, null, 0, Timeout.Infinite);
			_logger.Verbose("Notification monitor started.");
		}

		public void Stop()
		{
			_timerEnabled = false;
			_cancelToken.Cancel();
			if (_mainTimer != null)
			{
				_mainTimer.Dispose();
				_mainTimer = null;
			}
			_logger.Verbose("Notification monitor stopped.");
		}

		private async void TimerCallback(object state)
		{
			var parser = new EventParser();
			_logger.Verbose("Waiting for next monitor event...");
			try
			{
				if (_lastEventId == 0)
				{
					_lastEventId = await _networkService.WinChattyGetNewestEventId(_cancelToken.Token);
				}

				var jEvents = await _networkService.WinChattyWaitForEvent(_lastEventId, _cancelToken.Token);

				var newPostEvents = parser.GetNewPostEvents(jEvents);
				foreach (var newPost in newPostEvents)
				{
					var handler = _createHandlerFunc();
					await handler.ProcessEvent(newPost);
				}

				_lastEventId = parser.GetLatestEventId(jEvents);

				_timeDelay = 0;
			}
			catch (Exception ex)
			{
				if ((int)_timeDelay == 0)
				{
					_timeDelay = BaseTimeDelay;
				}
				//There was a problem, delay further.  To a maximum of 10 minutes.
				_timeDelay = Math.Min(Math.Pow(_timeDelay, TimeDelayFailExponent), 600);
				if (ex is TaskCanceledException)
				{
					//This is expected, we'll still slow down our polling of winchatty if the chatty's not busy but won't print a full stack.
					//Don't reset the event ID though, since nothing happened.  Don't want to miss events.
					_logger.Verbose("Timed out waiting for winchatty.");
				}
				else
				{
					//If there was an error, reset the event ID to 0 so we get the latest, otherwise we might get stuck in a loop where the API won't return us events because there are too many.
					_lastEventId = 0;
					_logger.Error(ex, $"Exception in {nameof(TimerCallback)}");
				}
			}
			finally
			{
				if (_timerEnabled)
				{
					_logger.Verbose("Delaying next monitor for {monitorDelay}ms", _timeDelay * 1000);
					_mainTimer.Change((int)(_timeDelay * 1000), Timeout.Infinite);
				}
			}
		}



		#region IDisposable Support
		private bool _disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (_mainTimer != null)
					{
						_mainTimer.Dispose();
					}
					if (_cancelToken != null)
					{
						_cancelToken.Dispose();
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_disposedValue = true;
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
