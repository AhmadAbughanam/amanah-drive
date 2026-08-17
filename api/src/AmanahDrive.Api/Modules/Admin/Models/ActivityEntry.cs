namespace AmanahDrive.Api.Modules.Admin.Models;

public sealed class ActivityEntry
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string Summary { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? FileId { get; set; }

    public Guid? ConversationId { get; set; }
}
