using AmanahDrive.Api.Shared.DomainEvents;

namespace AmanahDrive.Api.Modules.Processing.Events;

public sealed record ProcessingFailedEvent(
    Guid FileId,
    string FileName,
    DateTimeOffset OccurredAt) : IDomainEvent;
