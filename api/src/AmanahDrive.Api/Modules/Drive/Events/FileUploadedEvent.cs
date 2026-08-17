using AmanahDrive.Api.Shared.DomainEvents;

namespace AmanahDrive.Api.Modules.Drive.Events;

public sealed record FileUploadedEvent(
    Guid FileId,
    string FileName,
    DateTimeOffset OccurredAt) : IDomainEvent;
