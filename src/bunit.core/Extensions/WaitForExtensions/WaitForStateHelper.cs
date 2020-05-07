using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{
	/// <summary>
	/// Represents an async wait helper, that will wait for a specified time for a state predicate to pass.
	/// </summary>
	public class WaitForStateHelper : IDisposable
	{
		private readonly IRenderedFragmentBase _renderedFragment;
		private readonly Func<bool> _statePredicate;
		private readonly Timer _timer;
		private readonly ILogger _logger;
		private readonly TaskCompletionSource<bool> _completionSouce;
		private bool _disposed = false;

		/// <summary>
		/// Gets the task that will complete successfully if the state predicate returned true before the timeout was reached.
		/// The task will complete with an <see cref="WaitForStateFailedException"/> exception if the timeout was reached without the state predicate returning true,
		/// or if the state predicate throw an exception during invocation.
		/// </summary>
		public Task WaitTask => _completionSouce.Task;

		/// <summary>
		/// Creates an instance of the <see cref="WaitForStateHelper"/> type,
		/// which will wait until the provided <paramref name="statePredicate"/> action returns true,
		/// or the <paramref name="timeout"/> is reached (default is one second).
		/// 
		/// The <paramref name="statePredicate"/> is evaluated initially, and then each time the <paramref name="renderedFragment"/> renders.
		/// </summary>
		/// <param name="renderedFragment">The render fragment or component to attempt to verify state against.</param>
		/// <param name="statePredicate">The predicate to invoke after each render, which must returns <c>true</c> when the desired state has been reached.</param>
		/// <param name="timeout">The maximum time to wait for the desired state.</param>
		/// <exception cref="WaitForStateFailedException">Thrown if the <paramref name="statePredicate"/> throw an exception during invocation, or if the timeout has been reached. See the inner exception for details.</exception>
		public WaitForStateHelper(IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeout = null)
		{
			_logger = GetLogger<WaitForStateHelper>(renderedFragment.Services);
			_completionSouce = new TaskCompletionSource<bool>();
			_renderedFragment = renderedFragment;
			_statePredicate = statePredicate;
			_renderedFragment.OnAfterRender += TryPredicate;
			_timer = new Timer(HandleTimeout, this, timeout.GetRuntimeTimeout(), Timeout.InfiniteTimeSpan);
			TryPredicate();
		}

		private void TryPredicate()
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
					var error = new WaitForStateFailedException(WaitForStateFailedException.TIMEOUT_BEFORE_PASS, ex);
					_completionSouce.TrySetException(error);
					Dispose();
				}
			}
		}

		private void HandleTimeout(object state)
		{
			if (_disposed)
				return;

			lock (_completionSouce)
			{
				if (_disposed)
					return;

				_logger.LogDebug(new EventId(5, nameof(HandleTimeout)), $"The state wait helper for component {_renderedFragment.ComponentId} timed out");

				var error = new WaitForStateFailedException(WaitForStateFailedException.TIMEOUT_BEFORE_PASS);
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
