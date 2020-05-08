using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Xunit.Sdk;

namespace Bunit
{
	/// <summary>
	/// Base test class/test runner, that runs Fixtures defined in razor files.
	/// </summary>
	public abstract class TestComponentBase : IComponent
	{
		/// <summary>
		/// Renders the component to the supplied <see cref="RenderTreeBuilder"/>.
		/// </summary>
		/// <param name="builder">The builder to use for rendering.</param>
		protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

		/// <summary>
		/// Called by the XUnit test runner. Finds all Fixture components
		/// in the file and runs their associated tests.
		/// </summary>
		[RazorTest] public virtual void RazorTests() { }

		void IComponent.Attach(RenderHandle renderHandle) => renderHandle.Render(BuildRenderTree);

		Task IComponent.SetParametersAsync(ParameterView parameters) => Task.CompletedTask;
	}
}
