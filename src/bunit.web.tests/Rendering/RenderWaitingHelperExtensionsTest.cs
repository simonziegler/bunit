using System;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit.Mocking.JSInterop;
using Bunit.TestAssets.SampleComponents;
using Bunit.TestAssets.SampleComponents.Data;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Bunit.Rendering
{
	public class RenderWaitingHelperExtensionsTest : ComponentTestFixture
	{
		ITestOutputHelper _testOutput;
		public RenderWaitingHelperExtensionsTest(ITestOutputHelper testOutput)
		{
			Services.AddXunitLogger(testOutput);
			_testOutput = testOutput;
		}

		[Fact(DisplayName = "WaitForAssertion can wait for multiple renders and changes to occur")]
		public void Test110()
		{
			// Initial state is stopped
			var cut = RenderComponent<TwoRendersTwoChanges>();
			var stateElement = cut.Find("#state");
			stateElement.TextContent.ShouldBe("Stopped");

			// Clicking 'tick' changes the state, and starts a task
			cut.Find("#tick").Click();
			cut.Find("#state").TextContent.ShouldBe("Started");

			// Clicking 'tock' completes the task, which updates the state
			// This click causes two renders, thus something is needed to await here.
			cut.Find("#tock").Click();
			cut.WaitForAssertion(
				() => cut.Find("#state").TextContent.ShouldBe("Stopped")
			);
		}

		[Fact(DisplayName = "WaitForAssertion throws verification exception after timeout")]
		public void Test011()
		{
			const string expectedMessage = "The assertion did not pass within the timeout period.";
			var cut = RenderComponent<Simple1>();

			var expected = Should.Throw<WaitForAssertionFailedException>(() =>
			  cut.WaitForAssertion(() => cut.Markup.ShouldBeEmpty(), TimeSpan.FromMilliseconds(10))
			);

			expected.Message.ShouldBe(expectedMessage);
			expected.InnerException.ShouldBeOfType<ShouldAssertException>();
		}

		[Fact(DisplayName = "WaitForState throws WaitForRenderFailedException exception after timeout")]
		public void Test012()
		{
			const string expectedMessage = "The state predicate did not pass before the timeout period passed.";
			var cut = RenderComponent<Simple1>();

			var expected = Should.Throw<WaitForStateFailedException>(() =>
				cut.WaitForState(() => string.IsNullOrEmpty(cut.Markup), TimeSpan.FromMilliseconds(100))
			);

			expected.Message.ShouldBe(expectedMessage);
		}

		[Fact(DisplayName = "WaitForState throws WaitForStateFailedException exception if statePredicate throws on a later render")]
		public void Test013()
		{
			const string expectedMessage = "The state predicate did not pass before the timeout period passed.";
			const string expectedInnerMessage = "INNER MESSAGE";
			var cut = RenderComponent<TwoRendersTwoChanges>();
			cut.Find("#tick").Click();
			cut.Find("#tock").Click();

			var expected = Should.Throw<WaitForStateFailedException>(() =>
				cut.WaitForState(() =>
				{
					if (cut.Find("#state").TextContent == "Stopped")
						throw new InvalidOperationException(expectedInnerMessage);
					return false;
				})
			);

			expected.Message.ShouldBe(expectedMessage);
			expected.InnerException.ShouldBeOfType<InvalidOperationException>()
				.Message.ShouldBe(expectedInnerMessage);
		}

		[Fact(DisplayName = "WaitForState can wait for multiple renders and changes to occur")]
		public void Test100()
		{
			// Initial state is stopped
			var cut = RenderComponent<TwoRendersTwoChanges>();
			var stateElement = cut.Find("#state");
			stateElement.TextContent.ShouldBe("Stopped");

			// Clicking 'tick' changes the state, and starts a task
			cut.Find("#tick").Click();
			cut.Find("#state").TextContent.ShouldBe("Started");

			// Clicking 'tock' completes the task, which updates the state
			// This click causes two renders, thus something is needed to await here.
			cut.Find("#tock").Click();
			cut.WaitForState(() => cut.Find("#state").TextContent == "Stopped");

			cut.Find("#state").TextContent.ShouldBe("Stopped");
		}

		[Fact(DisplayName = "WaitForState can detect async changes to properties in the CUT")]
		public void Test200()
		{
			var cut = RenderComponent<AsyncRenderChangesProperty>();
			cut.Instance.Counter.ShouldBe(0);

			// Clicking 'tick' changes the counter, and starts a task
			cut.Find("#tick").Click();
			cut.Instance.Counter.ShouldBe(1);

			// Clicking 'tock' completes the task, which updates the counter
			// This click causes two renders, thus something is needed to await here.
			cut.Find("#tock").Click();
			cut.WaitForState(() => cut.Instance.Counter == 2);

			cut.Instance.Counter.ShouldBe(2);
		}
	}
}
