using Bunit.Rendering;
using Bunit.Rendering.RenderEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Bunit
{
	/// <summary>
	/// A test context is a factory that makes it possible to create components under tests.
	/// </summary>
	public class TestContextBase : ITestContext, IRenderEventHandler, IDisposable
	{
		private ITestRenderer? _testRenderer;

		/// <inheritdoc/>
		public event Action? OnAfterRender;

		/// <inheritdoc/>
		public ITestRenderer Renderer
		{
			get
			{
				if (_testRenderer is null)
				{
					_testRenderer = Services.GetRequiredService<ITestRenderer>();
					_testRenderer.AddRenderEventHandler(this);
				}
				return _testRenderer;
			}
		}

		/// <inheritdoc/>
		public virtual TestServiceProvider Services { get; }

		/// <summary>
		/// Creates a new instance of the <see cref="ITestContext"/> class.
		/// </summary>
		public TestContextBase()
		{
			Services = new TestServiceProvider();
			Services.AddSingleton<ITestRenderer>(srv => new TestRenderer(srv, srv.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			_testRenderer?.RemoveRenderEventHandler(this);
			Services.Dispose();
		}

		Task IRenderEventHandler.Handle(RenderEvent renderEvent)
		{
			OnAfterRender?.Invoke();
			return Task.CompletedTask;
		}
	}
}
