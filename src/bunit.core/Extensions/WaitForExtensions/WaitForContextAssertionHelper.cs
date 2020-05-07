using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{
	public class WaitForContextAssertionHelper : IDisposable
	{
		private readonly ITestContext _testContext;
		private readonly Action _assertion;
		private readonly Timer _timer;
		private readonly ILogger _logger;
		private readonly TaskCompletionSource<object?> _completionSouce;
		private bool _disposed = false;
		private Exception? _capturedException;

		public Task WaitTask => _completionSouce.Task;

		public WaitForContextAssertionHelper(ITestContext testContext, Action assertion, TimeSpan? timeout = null)
		{
			_logger = GetLogger<WaitForContextAssertionHelper>(testContext.Services);
			_completionSouce = new TaskCompletionSource<object?>();
			_testContext = testContext;
			_assertion = assertion;
			_timer = new Timer(HandleTimeout, this, timeout.GetRuntimeTimeout(), TimeSpan.FromMilliseconds(Timeout.Infinite));
			_testContext.OnAfterRender += TryAssertion;
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
				_logger.LogDebug(new EventId(1, nameof(TryAssertion)), $"Trying the assertion for the test context");

				try
				{
					_assertion();
					_capturedException = null;
					_completionSouce.TrySetResult(null);
					_logger.LogDebug(new EventId(2, nameof(TryAssertion)), $"The assertion for the test context passed");
					Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogDebug(new EventId(3, nameof(TryAssertion)), $"The assertion for the test context did not pass. The error message was '{ex.Message}'");
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

				_logger.LogDebug(new EventId(5, nameof(HandleTimeout)), $"The assertion wait helper for the test context timed out");

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
			_testContext.OnAfterRender -= TryAssertion;
			_timer.Dispose();
			_logger.LogDebug(new EventId(6, nameof(Dispose)), $"The state wait helper for the test context disposed");
			_completionSouce.TrySetCanceled();
		}

		private static ILogger<T> GetLogger<T>(IServiceProvider services)
		{
			var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
			return loggerFactory.CreateLogger<T>();
		}
	}
}
