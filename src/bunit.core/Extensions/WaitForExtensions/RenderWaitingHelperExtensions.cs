using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Bunit.Rendering.RenderEvents;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bunit
{

	/// <summary>
	/// Helper methods dealing with async rendering during testing.
	/// </summary>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
	public static class RenderWaitingHelperExtensions
	{
		///// <summary>
		///// Wait for the next render to happen, or the <paramref name="timeout"/> is reached (default is one second).
		///// If a <paramref name="renderTrigger"/> action is provided, it is invoked before the waiting.
		///// </summary>
		///// <param name="testContext">The test context to wait for renders from.</param>
		///// <param name="renderTrigger">The action that somehow causes one or more components to render.</param>
		///// <param name="timeout">The maximum time to wait for the next render. If not provided the default is 1 second. During debugging, the timeout is automatically set to infinite.</param>        
		///// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		///// <exception cref="WaitForRenderFailedException">Thrown if no render happens within the specified <paramref name="timeout"/>, or the default of 1 second, if non is specified.</exception>
		//[Obsolete("Use either the WaitForState or WaitForAssertion method instead. It will make your test more resilient to insignificant changes, as they will wait across multiple renders instead of just one. To make the change, run any render trigger first, then call either WaitForState or WaitForAssertion with the appropriate input. This method will be removed before the 1.0.0 release.", false)]
		//public static void WaitForNextRender(this ITestContext testContext, Action? renderTrigger = null, TimeSpan? timeout = null)
		//	=> WaitForRender(testContext, renderTrigger, timeout);

		///// <summary>
		///// Wait until the provided <paramref name="statePredicate"/> action returns true,
		///// or the <paramref name="timeout"/> is reached (default is one second).
		///// 
		///// The <paramref name="statePredicate"/> is evaluated initially, and then each time
		///// the renderer in the <paramref name="testContext"/> renders.
		///// </summary>
		///// <param name="testContext">The test context to wait for renders from.</param>
		///// <param name="statePredicate">The predicate to invoke after each render, which returns true when the desired state has been reached.</param>
		///// <param name="timeout">The maximum time to wait for the desired state.</param>
		///// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		///// <exception cref="WaitForStateFailedException">Thrown if the <paramref name="statePredicate"/> throw an exception during invocation, or if the timeout has been reached. See the inner exception for details.</exception>
		//public static void WaitForState(this ITestContext testContext, Func<bool> statePredicate, TimeSpan? timeout = null)
		//	=> WaitForState(testContext, statePredicate, timeout);

		///// <summary>
		///// Wait until the provided <paramref name="assertion"/> action passes (i.e. does not throw an 
		///// assertion exception), or the <paramref name="timeout"/> is reached (default is one second).
		///// 
		///// The <paramref name="assertion"/> is attempted initially, and then each time
		///// the renderer in the <paramref name="testContext"/> renders.
		///// </summary>
		///// <param name="testContext">The test context to wait for renders from.</param>
		///// <param name="assertion">The verification or assertion to perform.</param>
		///// <param name="timeout">The maximum time to attempt the verification.</param>
		///// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		///// <exception cref="WaitForAssertionFailedException">Thrown if the timeout has been reached. See the inner exception to see the captured assertion exception.</exception>
		//public static void WaitForAssertion(this ITestContext testContext, Action assertion, TimeSpan? timeout = null)
		//	=> WaitForAssertion(testContext, assertion, timeout);

		public static void WaitForState(this IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeout = null)
		{
			using var waiter = new WaitForStateHelper(renderedFragment, statePredicate, timeout);
			waiter.WaitTask.Wait();
		}

		private class WaitForStateHelper : IDisposable
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
				if (_disposed) return;
				lock (_completionSouce)
				{
					if (_disposed) return;
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
				if (_disposed) return;

				lock (_completionSouce)
				{
					if (_disposed) return;

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

		///// <summary>
		///// Wait until the provided <paramref name="statePredicate"/> action returns true,
		///// or the <paramref name="timeout"/> is reached (default is one second).
		///// The <paramref name="statePredicate"/> is evaluated initially, and then each time
		///// the <paramref name="renderedFragment"/> renders.
		///// </summary>
		///// <param name="renderedFragment">The rendered fragment to wait for renders from.</param>
		///// <param name="statePredicate">The predicate to invoke after each render, which returns true when the desired state has been reached.</param>
		///// <param name="timeout">The maximum time to wait for the desired state.</param>
		///// <exception cref="ArgumentNullException">Thrown if <paramref name="renderedFragment"/> is null.</exception>
		///// <exception cref="WaitForStateFailedException">Thrown if the <paramref name="statePredicate"/> throw an exception during invocation, or if the timeout has been reached. See the inner exception for details.</exception>
		//public static void WaitForState(this IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeout = null)
		//	=> WaitForStateAsync(renderedFragment, statePredicate, timeout).Wait(timeout.GetRuntimeTimeout());

		internal static Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
		{
			return timeout == Timeout.InfiniteTimeSpan
				? task
				: TaskOrTimeout(task, timeout);

			static async Task<TResult> TaskOrTimeout(Task<TResult> task, TimeSpan timeout)
			{
				using var timeoutCancellationTokenSource = new CancellationTokenSource();

				var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
				if (completedTask == task)
				{
					timeoutCancellationTokenSource.Cancel();
					return await task.ConfigureAwait(false);  // Very important in order to propagate exceptions
				}
				else
				{
					throw new TimeoutException();
				}
			}
		}

		public static async Task WaitForStateAsync(this IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeoutOrDefault = null)
		{
			const int STATE_MISMATCH = 0;
			const int STATE_MATCH = 1;
			const int STATE_EXCEPTION = -1;

			var timeout = timeoutOrDefault.GetRuntimeTimeout();
			var nextWaitTime = timeout;
			var stopWatch = new Stopwatch();
			var lastSeenRenderNum = renderedFragment.RenderCount;
			var failure = default(Exception);
			var status = STATE_MISMATCH;

			status = TryPredicate();

			while (status != STATE_MATCH && timeout > stopWatch.Elapsed)
			{
				nextWaitTime = GetNextWaitTime(timeout, stopWatch.Elapsed);
				stopWatch.Start();

				// If the last seen render number is the same as the currently
				// reported one, we wait for the next render to happen.
				// Otherwise, a render happened between the last seen render and the
				// next one we can await, so we skip awaiting and go straigt to
				// trying the predicate.
				if (lastSeenRenderNum == renderedFragment.RenderCount)
				{
					// TimeoutAfter can both throw a TimeoutException, expected after the timeout happens
					// or any other exceptions produced by the NextRender task. If any other exception is
					// thrown it is unexpected and should just be returned to the caller.
					try
					{
						lastSeenRenderNum = await renderedFragment.NextRender.TimeoutAfter(nextWaitTime);
					}
					catch (TimeoutException e)
					{
						failure = e;
						break;
					}
				}
				else
				{
					lastSeenRenderNum = renderedFragment.RenderCount;
				}

				stopWatch.Stop();
				status = TryPredicate();
			}

			// Report status to caller or just return
			switch (status)
			{
				case STATE_MATCH:
					return;
				case STATE_MISMATCH when failure is TimeoutException:
					throw new WaitForStateFailedException(WaitForStateFailedException.TIMEOUT_BEFORE_PASS, failure);
				case STATE_EXCEPTION when failure is { }:
					throw new WaitForStateFailedException(WaitForStateFailedException.EXCEPTION_IN_PREDICATE, failure);
			}

			int TryPredicate()
			{
				try
				{
					if (statePredicate())
						return STATE_MATCH;
					else
						return STATE_MISMATCH;
				}
				catch (Exception e)
				{
					failure = e;
					return STATE_EXCEPTION;
				}
			}

			TimeSpan GetNextWaitTime(TimeSpan timeout, TimeSpan elapsedTime)
			{
				return timeout - elapsedTime;
			}
		}

		/// <summary>
		/// Wait until the provided <paramref name="assertion"/> action passes (i.e. does not throw an 
		/// assertion exception), or the <paramref name="timeout"/> is reached (default is one second).
		/// 
		/// The <paramref name="assertion"/> is attempted initially, and then each time
		/// the <paramref name="renderedFragment"/> renders.
		/// </summary>
		/// <param name="renderedFragment">The rendered fragment to wait for renders from.</param>
		/// <param name="assertion">The verification or assertion to perform.</param>
		/// <param name="timeout">The maximum time to attempt the verification.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="renderedFragment"/> is null.</exception>
		/// <exception cref="WaitForAssertionFailedException">Thrown if the timeout has been reached. See the inner exception to see the captured assertion exception.</exception>
		public static void WaitForAssertion(this IRenderedFragmentBase renderedFragment, Action assertion, TimeSpan? timeout = null)
		{
			//try
			//{
			WaitForAssertionAsync(renderedFragment, assertion, timeout).Wait(timeout.GetRuntimeTimeout());
			//}
			//catch (AggregateException ex)
			//{
			//	throw ex.InnerException;
			//}
		}

		private static ILogger GetLogger(IServiceProvider services, string loggerCategory)
		{
			var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
			return loggerFactory.CreateLogger(loggerCategory);
		}

		public static async Task WaitForAssertionAsync(this IRenderedFragmentBase renderedFragment, Action assertion, TimeSpan? timeoutOrDefault = null)
		{
			const int FAILED = 1;
			const int PASSED = 2;

			var logger = GetLogger(renderedFragment.Services, nameof(WaitForAssertionAsync));

			var timeout = timeoutOrDefault.GetRuntimeTimeout();
			var nextWaitTime = timeout;
			var stopWatch = new Stopwatch();
			var lastSeenRenderNum = renderedFragment.RenderCount;
			var failure = default(Exception);
			var status = FAILED;

			status = TryAssertion();

			while (status == FAILED && timeout > stopWatch.Elapsed)
			{
				nextWaitTime = GetNextWaitTime(timeout, stopWatch.Elapsed);
				stopWatch.Start();

				// If the last seen render number is the same as the currently
				// reported one, we wait for the next render to happen.
				// Otherwise, a render happened between the last seen render and the
				// next one we can await, so we skip awaiting and go straigt to
				// trying the predicate.
				if (lastSeenRenderNum == renderedFragment.RenderCount)
				{
					// TimeoutAfter can both throw a TimeoutException, expected after the timeout happens
					// or any other exceptions produced by the NextRender task. If any other exception is
					// thrown it is unexpected and should just be returned to the caller.
					try
					{
						logger.LogDebug($"Waiting for next render from component {renderedFragment.ComponentId}. LastSeenRenderNum = {lastSeenRenderNum}");
						lastSeenRenderNum = await renderedFragment.NextRender.TimeoutAfter(nextWaitTime).ConfigureAwait(false);

					}
					catch (TimeoutException e)
					{
						failure = e;
						break;
					}
				}
				else
				{
					lastSeenRenderNum = renderedFragment.RenderCount;
				}

				stopWatch.Stop();
				logger.LogDebug($"Next render received from component {renderedFragment.ComponentId}. LastSeenRenderNum = {lastSeenRenderNum}");
				status = TryAssertion();
			}

			// Report status to caller or just return
			switch (status)
			{
				case PASSED:
					return;
				case FAILED when failure is { }:
					throw new WaitForAssertionFailedException(failure);
				case FAILED:
					throw new Exception("NOT SUPPOSED TO HAPPEN!. FAILED ASSERTION BUT NO EXCEPTION!");
			}

			int TryAssertion()
			{
				try
				{
					assertion();
					failure = null;
					logger.LogDebug($"Assertion passed for {renderedFragment.ComponentId}. LastSeenRenderNum = {lastSeenRenderNum}");
					return PASSED;
				}
				catch (Exception e)
				{
					logger.LogDebug($"Assertion attempt failed {renderedFragment.ComponentId}. LastSeenRenderNum = {lastSeenRenderNum}");
					failure = e;
					return FAILED;
				}
			}

			TimeSpan GetNextWaitTime(TimeSpan timeout, TimeSpan elapsedTime)
			{
				return timeout == Timeout.InfiniteTimeSpan
					? timeout
					: timeout - elapsedTime;
			}
		}

		//private static void WaitForRender(ITestContext testContext, Action? renderTrigger = null, TimeSpan? timeout = null)
		//{
		//	var waitTime = timeout.GetRuntimeTimeout();



		//	//testContext.Renderer.AddRenderEventHandler()


		//	//var waitTime = timeout.GetRuntimeTimeout();

		//	//var rvs = new ConcurrentRenderEventSubscriber(renderEventObservable);

		//	//try
		//	//{
		//	//	renderTrigger?.Invoke();

		//	//	if (rvs.RenderCount > 0)
		//	//		return;

		//	//	// RenderEventSubscriber (rvs) receive render events on the renderer's thread, where as 
		//	//	// the WaitForNextRender is started from the test runners thread.
		//	//	// Thus it is safe to SpinWait on the test thread and wait for the RenderCount to go above 0.
		//	//	if (SpinWait.SpinUntil(ShouldSpin, waitTime) && rvs.RenderCount > 0)
		//	//		return;
		//	//	else
		//	//		throw new WaitForRenderFailedException();
		//	//}
		//	//finally
		//	//{
		//	//	rvs.Unsubscribe();
		//	//}

		//	//bool ShouldSpin() => rvs.RenderCount > 0 || rvs.IsCompleted;
		//}

		//private static void WaitForState(IObservable<RenderEvent> renderEventObservable, Func<bool> statePredicate, TimeSpan? timeout = null)
		//{
		//	if (renderEventObservable is null)
		//		throw new ArgumentNullException(nameof(renderEventObservable));
		//	if (statePredicate is null)
		//		throw new ArgumentNullException(nameof(statePredicate));

		//	const int STATE_MISMATCH = 0;
		//	const int STATE_MATCH = 1;
		//	const int STATE_EXCEPTION = -1;

		//	var spinTime = timeout.GetRuntimeTimeout();
		//	var failure = default(Exception);
		//	var status = STATE_MISMATCH;

		//	var rvs = new ConcurrentRenderEventSubscriber(renderEventObservable, onRender: TryVerification);
		//	try
		//	{
		//		TryVerification();
		//		WaitingResultHandler(continueIfMisMatch: true);

		//		// ComponentChangeEventSubscriber (rvs) receive render events on the renderer's thread, where as 
		//		// the VerifyAsyncChanges is started from the test runners thread.
		//		// Thus it is safe to SpinWait on the test thread and wait for verification to pass.
		//		// When a render event is received by rvs, the verification action will execute on the
		//		// renderer thread.
		//		// 
		//		// Therefore, we use Volatile.Read/Volatile.Write in the helper methods below to ensure
		//		// that an update to the variable status is not cached in a local CPU, and 
		//		// not available in a secondary CPU, if the two threads are running on a different CPUs
		//		SpinWait.SpinUntil(ShouldSpin, spinTime);
		//		WaitingResultHandler(continueIfMisMatch: false);
		//	}
		//	finally
		//	{
		//		rvs.Unsubscribe();
		//	}

		//	void WaitingResultHandler(bool continueIfMisMatch)
		//	{
		//		switch (status)
		//		{
		//			case STATE_MATCH:
		//				return;
		//			case STATE_MISMATCH when !continueIfMisMatch && failure is null:
		//				throw WaitForStateFailedException.CreateNoMatchBeforeTimeout();
		//			case STATE_EXCEPTION when failure is { }:
		//				throw WaitForStateFailedException.CreatePredicateThrowException(failure);
		//		}
		//	}

		//	void TryVerification(RenderEvent _ = default!)
		//	{
		//		try
		//		{
		//			if (statePredicate())
		//				Volatile.Write(ref status, STATE_MATCH);
		//		}
		//		catch (Exception e)
		//		{
		//			failure = e;
		//			Volatile.Write(ref status, STATE_EXCEPTION);
		//		}
		//	}

		//	bool ShouldSpin() => Volatile.Read(ref status) == STATE_MATCH || rvs.IsCompleted;
		//}

		private static void WaitForAssertion(IObservable<RenderEvent> renderEventObservable, Action assertion, TimeSpan? timeout = null)
		{
			if (renderEventObservable is null)
				throw new ArgumentNullException(nameof(renderEventObservable));
			if (assertion is null)
				throw new ArgumentNullException(nameof(assertion));

			const int FAILING = 0;
			const int PASSED = 1;

			var spinTime = timeout.GetRuntimeTimeout();
			var failure = default(Exception);
			var status = FAILING;

			var rvs = new ConcurrentRenderEventSubscriber(renderEventObservable, onRender: TryVerification);
			try
			{
				TryVerification();
				if (status == PASSED)
					return;

				// HasChangesRenderEventSubscriber (rvs) receive render events on the renderer's thread, where as 
				// the VerifyAsyncChanges is started from the test runners thread.
				// Thus it is safe to SpinWait on the test thread and wait for verification to pass.
				// When a render event is received by rvs, the verification action will execute on the
				// renderer thread.
				// 
				// Therefore, we use Volatile.Read/Volatile.Write in the helper methods below to ensure
				// that an update to the variable status is not cached in a local CPU, and 
				// not available in a secondary CPU, if the two threads are running on a different CPUs
				SpinWait.SpinUntil(ShouldSpin, spinTime);

				if (status == FAILING && failure is { })
				{
					throw new WaitForAssertionFailedException(failure);
				}
			}
			finally
			{
				rvs.Unsubscribe();
			}

			void TryVerification(RenderEvent _ = default!)
			{
				try
				{
					assertion();
					Volatile.Write(ref status, PASSED);
					failure = null;
				}
				catch (Exception e)
				{
					failure = e;
				}
			}

			bool ShouldSpin() => Volatile.Read(ref status) == PASSED || rvs.IsCompleted;
		}


	}
}
