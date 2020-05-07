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
		/// <summary>
		/// Wait for the next render to happen, or the <paramref name="timeout"/> is reached (default is one second).
		/// If a <paramref name="renderTrigger"/> action is provided, it is invoked before the waiting.
		/// </summary>
		/// <param name="testContext">The test context to wait for renders from.</param>
		/// <param name="renderTrigger">The action that somehow causes one or more components to render.</param>
		/// <param name="timeout">The maximum time to wait for the next render. If not provided the default is 1 second. During debugging, the timeout is automatically set to infinite.</param>        
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		/// <exception cref="WaitForRenderFailedException">Thrown if no render happens within the specified <paramref name="timeout"/>, or the default of 1 second, if non is specified.</exception>
		[Obsolete("Use either the WaitForState or WaitForAssertion method instead. It will make your test more resilient to insignificant changes, as they will wait across multiple renders instead of just one. To make the change, run any render trigger first, then call either WaitForState or WaitForAssertion with the appropriate input. This method will be removed before the 1.0.0 release.", false)]
		public static void WaitForNextRender(this ITestContext testContext, Action? renderTrigger = null, TimeSpan? timeout = null)
		{
			using var waiter = new WaitForRenderHelper(testContext, 1, timeout);
			try
			{
				renderTrigger?.Invoke();
				waiter.WaitTask.Wait();
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
			}
		}

		/// <summary>
		/// Wait until the provided <paramref name="statePredicate"/> action returns true,
		/// or the <paramref name="timeout"/> is reached (default is one second).
		/// 
		/// The <paramref name="statePredicate"/> is evaluated initially, and then each time
		/// the renderer in the <paramref name="testContext"/> renders.
		/// </summary>
		/// <param name="testContext">The test context to wait for renders from.</param>
		/// <param name="statePredicate">The predicate to invoke after each render, which returns true when the desired state has been reached.</param>
		/// <param name="timeout">The maximum time to wait for the desired state.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		/// <exception cref="WaitForStateFailedException">Thrown if the <paramref name="statePredicate"/> throw an exception during invocation, or if the timeout has been reached. See the inner exception for details.</exception>
		public static void WaitForState(this ITestContext testContext, Func<bool> statePredicate, TimeSpan? timeout = null)
		{
			using var waiter = new WaitForContextStateHelper(testContext, statePredicate, timeout);
			try
			{
				waiter.WaitTask.Wait();
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
			}
		}

		/// <summary>
		/// Wait until the provided <paramref name="assertion"/> action passes (i.e. does not throw an 
		/// assertion exception), or the <paramref name="timeout"/> is reached (default is one second).
		/// 
		/// The <paramref name="assertion"/> is attempted initially, and then each time
		/// the renderer in the <paramref name="testContext"/> renders.
		/// </summary>
		/// <param name="testContext">The test context to wait for renders from.</param>
		/// <param name="assertion">The verification or assertion to perform.</param>
		/// <param name="timeout">The maximum time to attempt the verification.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="testContext"/> is null.</exception>
		/// <exception cref="WaitForAssertionFailedException">Thrown if the timeout has been reached. See the inner exception to see the captured assertion exception.</exception>
		public static void WaitForAssertion(this ITestContext testContext, Action assertion, TimeSpan? timeout = null)
		{
			using var waiter = new WaitForContextAssertionHelper(testContext, assertion, timeout);
			try
			{
				waiter.WaitTask.Wait();
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
			}
		}

		public static void WaitForState(this IRenderedFragmentBase renderedFragment, Func<bool> statePredicate, TimeSpan? timeout = null)
		{
			using var waiter = new WaitForStateHelper(renderedFragment, statePredicate, timeout);
			try
			{
				waiter.WaitTask.Wait();
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
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
			using var waiter = new WaitForAssertionHelper(renderedFragment, assertion, timeout);
			try
			{
				waiter.WaitTask.Wait();
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
			}
		}
	}
}
