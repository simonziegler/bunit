using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{
	public class WaitForRenderHelper : IDisposable
	{
		private readonly ITestContext _testContext;
		private readonly int _targetRenderCount;
		private readonly Timer _timer;
		private readonly ILogger _logger;
		private readonly TaskCompletionSource<object?> _completionSouce;
		private bool _disposed = false;
		private int _observedRenders;

		public Task WaitTask => _completionSouce.Task;

		public WaitForRenderHelper(ITestContext testContext, int targetRenderCount = 1, TimeSpan? timeout = null)
		{
			_logger = GetLogger<WaitForRenderHelper>(testContext.Services);
			_completionSouce = new TaskCompletionSource<object?>();
			_testContext = testContext;
			_targetRenderCount = targetRenderCount;
			_timer = new Timer(HandleTimeout, this, timeout.GetRuntimeTimeout(), TimeSpan.FromMilliseconds(Timeout.Infinite));
			_testContext.OnAfterRender += OnAfterRenderHandler;
		}

		void OnAfterRenderHandler()
		{
			if (_disposed)
				return;
			lock (_completionSouce)
			{
				if (_disposed)
					return;

				var renderCount = Interlocked.Increment(ref _observedRenders);

				_logger.LogDebug(new EventId(1, nameof(OnAfterRenderHandler)), $"A render has been observed from the test context. Total observed renders by the waiter is now {renderCount}");

				if (renderCount >= _targetRenderCount)
				{
					_logger.LogDebug(new EventId(2, nameof(OnAfterRenderHandler)), $"The target render count of {_targetRenderCount} from the test context has been reached");
					_completionSouce.TrySetResult(null);
					Dispose();
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

				_logger.LogDebug(new EventId(3, nameof(HandleTimeout)), $"The target render count of {_targetRenderCount} from the test context was not reached within the timeout limit");

				var error = new WaitForRenderFailedException();
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
			_testContext.OnAfterRender -= OnAfterRenderHandler;
			_timer.Dispose();
			_logger.LogDebug(new EventId(4, nameof(Dispose)), $"The render wait helper from the test context disposed");
			_completionSouce.TrySetCanceled();
		}

		private static ILogger<T> GetLogger<T>(IServiceProvider services)
		{
			var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
			return loggerFactory.CreateLogger<T>();
		}
	}
}
