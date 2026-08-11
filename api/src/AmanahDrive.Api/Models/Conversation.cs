namespace AmanahDrive.Api.Models;

public sealed class Conversation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AdminUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
}
