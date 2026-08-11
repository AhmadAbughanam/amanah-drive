namespace AmanahDrive.Api.Modules.SearchChat.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public required string Role { get; set; }

    public required string Content { get; set; }

    public string? CitationsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
