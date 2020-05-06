using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit.Rendering.RenderEvents;
using Bunit.TestAssets.SampleComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Bunit.Rendering
{
	public class TestRendererTest
	{
		private static readonly ServiceProvider ServiceProvider = new ServiceCollection().BuildServiceProvider();

		//[Fact(DisplayName = "Renderer pushes render events to subscribers when renders occur")]
		//public async Task Test001()
		//{
		//	// arrange
		//	var sut = new TestRenderer(ServiceProvider, NullLoggerFactory.Instance);
		//	var res = new ConcurrentRenderEventSubscriber(sut.RenderEvents);

		//	// act
		//	var cut = sut.RenderComponent<Simple1>(Array.Empty<ComponentParameter>());

		//	// assert
		//	res.RenderCount.ShouldBe(1);

		//	// act - trigger another render by setting the components parameters again
		//	await sut.InvokeAsync(() => cut.Component.SetParametersAsync(ParameterView.Empty));

		//	// assert
		//	res.RenderCount.ShouldBe(2);
		//}

		[Fact(DisplayName = "Renderer notifies handlers of render events")]
		public async Task Test001()
		{
			// Arrange
			var sut = new TestRenderer(ServiceProvider, NullLoggerFactory.Instance);
			var handler = new TestRenderEventHandler(completeHandleTaskSynchronously: true);
			sut.AddRenderEventHandler(handler);

			// Act #1
			var cut = sut.RenderComponent<Simple1>(Array.Empty<ComponentParameter>());

			// Assert #1
			handler.ReceivedEvents.Count.ShouldBe(1);

			// Act #2
			await sut.InvokeAsync(() => cut.Component.SetParametersAsync(ParameterView.Empty));

			// Assert #2
			handler.ReceivedEvents.Count.ShouldBe(2);
		}

		[Fact(DisplayName = "Multiple handlers can be added to the Renderer")]
		public void Test002()
		{
			var sut = new TestRenderer(ServiceProvider, NullLoggerFactory.Instance);
			var handler1 = new TestRenderEventHandler(completeHandleTaskSynchronously: true);
			var handler2 = new TestRenderEventHandler(completeHandleTaskSynchronously: true);

			sut.AddRenderEventHandler(handler1);
			sut.AddRenderEventHandler(handler2);

			sut.RenderComponent<Simple1>(Array.Empty<ComponentParameter>());
			handler1.ReceivedEvents.Count.ShouldBe(1);
			handler2.ReceivedEvents.Count.ShouldBe(1);
		}

		[Fact(DisplayName = "Handler is not invoked if removed from Renderer")]
		public void Test003()
		{
			var sut = new TestRenderer(ServiceProvider, NullLoggerFactory.Instance);
			var handler1 = new TestRenderEventHandler(completeHandleTaskSynchronously: true);
			var handler2 = new TestRenderEventHandler(completeHandleTaskSynchronously: true);
			sut.AddRenderEventHandler(handler1);
			sut.AddRenderEventHandler(handler2);

			sut.RemoveRenderEventHandler(handler1);

			sut.RenderComponent<Simple1>(Array.Empty<ComponentParameter>());
			handler1.ReceivedEvents.ShouldBeEmpty();
			handler2.ReceivedEvents.Count.ShouldBe(1);
		}

		//[Fact(DisplayName = "Renderer awaits handlers Task before rendering again")]
		//public async Task MyTestMethod()
		//{
		//	using var sut = new TestRenderer(ServiceProvider, NullLoggerFactory.Instance);
		//	var handler = new TestRenderEventHandler(completeHandleTaskSynchronously: true);

		//	var cut = sut.RenderComponent<TwoRendersTwoChanges>(Array.Empty<ComponentParameter>());
		//	cut.Component.state.ShouldBe("Stopped");

		//	sut.AddRenderEventHandler(handler);

		//	var tickDispatchTask = DispatchEventAsync(sut, cut.ComponentId, "Tick", new MouseEventArgs());

		//	cut.Component.state.ShouldBe("Started");
		//	tickDispatchTask.Status.ShouldBe(TaskStatus.WaitingForActivation);
		//	handler.ReceivedEvents.Count.ShouldBe(1);
		//	handler.SetCompleted();
		//	//handler.ReceivedEvents.Count.ShouldBe(2);

		//	var tockDispatchTask = DispatchEventAsync(sut, cut.ComponentId, "Tock", new MouseEventArgs());
		//	tickDispatchTask.Status.ShouldBe(TaskStatus.RanToCompletion);
		//	handler.ReceivedEvents.Count.ShouldBe(2);
		//	handler.SetCompleted();
		//	//handler.ReceivedEvents.Count.ShouldBe(4);
		//	cut.Component.state.ShouldBe("Stopped");
		//}

		private Task DispatchEventAsync<T>(TestRenderer renderer, int componentId, string handlerName, T eventArgs)
			where T : EventArgs
		{
			var (id, name) = FindEventHandlerId<T>(renderer.GetCurrentRenderTreeFrames(componentId), handlerName);
			return renderer.DispatchEventAsync(id, new EventFieldInfo() { FieldValue = name }, eventArgs);
		}

		private (ulong id, string name) FindEventHandlerId<T>(ArrayRange<RenderTreeFrame> frames, string handlerName)
		{
			for (int i = 0; i < frames.Count; i++)
			{
				ref var frame = ref frames.Array[i];
				if (frame.FrameType != RenderTreeFrameType.Attribute)
					continue;

				if (frame.AttributeEventHandlerId > 0)
				{
					switch (frame.AttributeValue)
					{
						case Action<T> h when h.Method.Name == handlerName:
						case Func<T, Task> h2 when h2.Method.Name == handlerName:
							return (frame.AttributeEventHandlerId, frame.AttributeName);
					}
				}
			}
			throw new Exception("Handler not found");
		}


		class TestRenderEventHandler : IRenderEventHandler
		{
			private TaskCompletionSource<object?> _handleTask = new TaskCompletionSource<object?>();
			private readonly bool _completeHandleTaskSynchronously;

			public List<RenderEvent> ReceivedEvents { get; set; } = new List<RenderEvent>();

			public TestRenderEventHandler(bool completeHandleTaskSynchronously)
			{
				if (completeHandleTaskSynchronously)
					SetCompleted();
				_completeHandleTaskSynchronously = completeHandleTaskSynchronously;
			}

			public Task Handle(RenderEvent renderEvent)
			{
				ReceivedEvents.Add(renderEvent);
				return _handleTask.Task;
			}

			public void SetCompleted()
			{
				if (_completeHandleTaskSynchronously)
					return;

				var existing = _handleTask;
				_handleTask = new TaskCompletionSource<object?>();
				existing.SetResult(null);
			}
		}

	}
}
