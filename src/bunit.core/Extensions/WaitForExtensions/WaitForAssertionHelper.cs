using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{

	public class WaitForAssertionHelper : IDisposable
	{
		private readonly IRenderedFragmentBase _renderedFragment;
		private readonly Action _assertion;
		private readonly Timer _timer;
		private readonly ILogger _logger;
		private readonly TaskCompletionSource<object?> _completionSouce;
		private bool _disposed = false;
		private Exception? _capturedException;

		public Task WaitTask => _completionSouce.Task;

		public WaitForAssertionHelper(IRenderedFragmentBase renderedFragment, Action assertion, TimeSpan? timeout = null)
		{
			_logger = GetLogger<WaitForAssertionHelper>(renderedFragment.Services);
			_completionSouce = new TaskCompletionSource<object?>();
			_renderedFragment = renderedFragment;
			_assertion = assertion;
			_timer = new Timer(HandleTimeout, this, timeout.GetRuntimeTimeout(), TimeSpan.FromMilliseconds(Timeout.Infinite));
			_renderedFragment.OnAfterRender += TryAssertion;
			TryAssertion();
		}

		void TryAssertion()
		{
			if (_disposed)
				return;
			lock (_completionSouce)
			{
				if (_disposed)
					return;
				_logger.LogDebug(new EventId(1, nameof(TryAssertion)), $"Trying the assertion for component {_renderedFragment.ComponentId}");

				try
				{
					_assertion();
					_capturedException = null;
					_completionSouce.TrySetResult(null);
					_logger.LogDebug(new EventId(2, nameof(TryAssertion)), $"The assertion for component {_renderedFragment.ComponentId} passed");
					Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogDebug(new EventId(3, nameof(TryAssertion)), $"The assertion for component {_renderedFragment.ComponentId} did not pass. The error message was '{ex.Message}'");
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

				_logger.LogDebug(new EventId(5, nameof(HandleTimeout)), $"The assertion wait helper for component {_renderedFragment.ComponentId} timed out");

				var error = new WaitForAssertionFailedException(_capturedException);
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
			_renderedFragment.OnAfterRender -= TryAssertion;
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
