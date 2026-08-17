using AmanahDrive.Api.Shared.DomainEvents;

namespace AmanahDrive.Api.Modules.SearchChat.Events;

public sealed record ChatAnsweredEvent(
    Guid ConversationId,
    string Question,
    DateTimeOffset OccurredAt) : IDomainEvent;
