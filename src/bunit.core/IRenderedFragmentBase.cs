using System;
using System.Threading.Tasks;
using Bunit.Rendering.RenderEvents;

namespace Bunit
{
	/// <summary>
	/// Represents a rendered fragment.
	/// </summary>
	public interface IRenderedFragmentBase
	{
		/// <summary>
		/// Gets the id of the rendered component or fragment.
		/// </summary>
		int ComponentId { get; }

		/// <summary>
		/// Gets the total number times the fragment has been through its render life-cycle.
		/// </summary>
		int RenderCount { get; }

		/// <summary>
		/// Gets a <see cref="Task{Int}"/>, that when completed, indicates that the <see cref="IRenderedFragmentBase"/>
		/// has been through a render life-cycle. The result of the task, indicates how many times the fragment
		/// has rendered in total.
		/// </summary>
		Task<int> NextRender { get; }

		/// <summary>
		/// An event that is raised after the markup of the <see cref="IRenderedFragmentBase"/> is updated.
		/// </summary>
		event Action OnMarkupUpdated;

		event Action OnAfterRender;

		/// <summary>
		/// Gets the <see cref="IServiceProvider"/> used when rendering the component.
		/// </summary>
		IServiceProvider Services { get; }
	}
}
