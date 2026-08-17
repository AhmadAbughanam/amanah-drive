namespace AmanahDrive.Api.Shared.DomainEvents;

public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : IDomainEvent
    {
        IDomainEventHandler<TEvent>[] handlers;
        try
        {
            handlers = serviceProvider.GetServices<IDomainEventHandler<TEvent>>().ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Domain event handlers could not be resolved for {EventType}; the originating operation will continue",
                typeof(TEvent).Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Domain event handler {HandlerType} failed for {EventType}; the originating operation will continue",
                    handler.GetType().Name,
                    typeof(TEvent).Name);
            }
        }
    }
}
