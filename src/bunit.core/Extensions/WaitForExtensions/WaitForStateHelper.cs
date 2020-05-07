using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{

	public class WaitForStateHelper : IDisposable
	{
		private readonly IRenderedFragmentBase _renderedFragment;
		private readonly Func<bool> _statePredicate;
		private readonly Timer _timer;
		private readonly ILogger _logger;
		private readonly TaskCompletionSource<bool> _completionSouce;
		private bool _disposed = false;
		private Exception? _capturedException;
		
		public Task WaitTask => _completionSouce.Task;

		public WaitForStateHelper(IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeout = null)
		{
			_logger = GetLogger<WaitForStateHelper>(renderedFragment.Services);
			_completionSouce = new TaskCompletionSource<bool>();
			_renderedFragment = renderedFragment;
			_statePredicate = statePredicate;
			_timer = new Timer(HandleTimeout, this, timeout.GetRuntimeTimeout(), TimeSpan.FromMilliseconds(Timeout.Infinite));
			_renderedFragment.OnAfterRender += TryPredicate;
			TryPredicate();
		}

		void TryPredicate()
		{
			if (_disposed)
				return;
			lock (_completionSouce)
			{
				if (_disposed)
					return;
				_logger.LogDebug(new EventId(1, nameof(TryPredicate)), $"Trying the state predicate for component {_renderedFragment.ComponentId}");

				try
				{
					var result = _statePredicate();
					if (result)
					{
						_logger.LogDebug(new EventId(2, nameof(TryPredicate)), $"The state predicate for component {_renderedFragment.ComponentId} passed");
						_completionSouce.TrySetResult(result);

						Dispose();
					}
					else
					{
						_logger.LogDebug(new EventId(3, nameof(TryPredicate)), $"The state predicate for component {_renderedFragment.ComponentId} did not pass");
					}
				}
				catch (Exception ex)
				{
					_logger.LogDebug(new EventId(4, nameof(TryPredicate)), $"The state predicate for component {_renderedFragment.ComponentId} throw an exception '{ex.GetType().Name}' with message '{ex.Message}'");
					_capturedException = ex;
				}
			}
		}

		void HandleTimeout(object state)
		{
			if (_disposed)
				return;

			lock (_completionSouce)
			{
				if (_disposed)
					return;

				_logger.LogDebug(new EventId(5, nameof(HandleTimeout)), $"The state wait helper for component {_renderedFragment.ComponentId} timed out");

				var error = new WaitForStateFailedException(WaitForStateFailedException.TIMEOUT_BEFORE_PASS, _capturedException);
				_completionSouce.TrySetException(error);

				Dispose();
			}
		}

		/// <summary>
		/// Disposes the wait helper and sets the <see cref="WaitTask"/> to canceled, if it is not
		/// already in one of the other completed states.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			_renderedFragment.OnAfterRender -= TryPredicate;
			_timer.Dispose();
			_logger.LogDebug(new EventId(6, nameof(Dispose)), $"The state wait helper for component {_renderedFragment.ComponentId} disposed");
			_completionSouce.TrySetCanceled();
		}

		private static ILogger<T> GetLogger<T>(IServiceProvider services)
		{
			var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
			return loggerFactory.CreateLogger<T>();
		}
	}
}
