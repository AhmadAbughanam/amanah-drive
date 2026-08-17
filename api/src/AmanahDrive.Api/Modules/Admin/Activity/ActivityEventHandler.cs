using AmanahDrive.Api.Modules.Admin.Models;
using AmanahDrive.Api.Modules.Drive.Events;
using AmanahDrive.Api.Modules.Processing.Events;
using AmanahDrive.Api.Modules.SearchChat.Events;
using AmanahDrive.Api.Shared.DomainEvents;
using AmanahDrive.Api.Shared.Infrastructure.Data;

namespace AmanahDrive.Api.Modules.Admin.Activity;

public sealed class ActivityEventHandler(AmanahDriveDbContext dbContext) :
    IDomainEventHandler<FileUploadedEvent>,
    IDomainEventHandler<ProcessingCompletedEvent>,
    IDomainEventHandler<ProcessingFailedEvent>,
    IDomainEventHandler<ChatAnsweredEvent>
{
    public Task HandleAsync(FileUploadedEvent domainEvent, CancellationToken cancellationToken) =>
        PersistAsync(
            ActivityTypes.FileUploaded,
            $"Uploaded {domainEvent.FileName}",
            domainEvent.OccurredAt,
            domainEvent.FileId,
            null,
            cancellationToken);

    public Task HandleAsync(ProcessingCompletedEvent domainEvent, CancellationToken cancellationToken) =>
        PersistAsync(
            ActivityTypes.ProcessingCompleted,
            $"Finished processing {domainEvent.FileName}",
            domainEvent.OccurredAt,
            domainEvent.FileId,
            null,
            cancellationToken);

    public Task HandleAsync(ProcessingFailedEvent domainEvent, CancellationToken cancellationToken) =>
        PersistAsync(
            ActivityTypes.ProcessingFailed,
            $"Failed processing {domainEvent.FileName}",
            domainEvent.OccurredAt,
            domainEvent.FileId,
            null,
            cancellationToken);

    public Task HandleAsync(ChatAnsweredEvent domainEvent, CancellationToken cancellationToken) =>
        PersistAsync(
            ActivityTypes.ChatAnswered,
            $"Answered: {Truncate(domainEvent.Question.Trim(), 470)}",
            domainEvent.OccurredAt,
            null,
            domainEvent.ConversationId,
            cancellationToken);

    private async Task PersistAsync(
        string type,
        string summary,
        DateTimeOffset occurredAt,
        Guid? fileId,
        Guid? conversationId,
        CancellationToken cancellationToken)
    {
        await dbContext.ActivityEntries.AddAsync(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            Type = type,
            Summary = Truncate(summary, 500),
            OccurredAt = occurredAt,
            FileId = fileId,
            ConversationId = conversationId
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 3)].TrimEnd() + "...";
}

public static class ActivityTypes
{
    public const string FileUploaded = "FileUploaded";
    public const string ProcessingCompleted = "ProcessingCompleted";
    public const string ProcessingFailed = "ProcessingFailed";
    public const string ChatAnswered = "ChatAnswered";
}
