using AmanahDrive.Api.Shared.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AmanahDrive.Api.Tests;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task Publish_WhenAHandlerFails_ContinuesToOtherHandlers()
    {
        var recordingHandler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(new ThrowingHandler());
        services.AddSingleton<IDomainEventHandler<TestEvent>>(recordingHandler);
        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(
            serviceProvider,
            NullLogger<DomainEventDispatcher>.Instance);

        await dispatcher.PublishAsync(new TestEvent(), CancellationToken.None);

        Assert.True(recordingHandler.WasCalled);
    }

    [Fact]
    public async Task Publish_WhenAHandlerCannotBeResolved_DoesNotFailThePublisher()
    {
        var services = new ServiceCollection();
        services.AddTransient<IDomainEventHandler<TestEvent>>(_ =>
            throw new InvalidOperationException("handler construction failed"));
        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(
            serviceProvider,
            NullLogger<DomainEventDispatcher>.Instance);

        await dispatcher.PublishAsync(new TestEvent(), CancellationToken.None);
    }

    private sealed record TestEvent : IDomainEvent;

    private sealed class ThrowingHandler : IDomainEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("activity handler failed");
    }

    private sealed class RecordingHandler : IDomainEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }
}
