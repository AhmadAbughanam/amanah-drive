using AmanahDrive.Api.Modules.SearchChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.SearchChat.Data;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> entity)
    {
        entity.ToTable("chat_messages");
        entity.HasKey(message => message.Id);
        entity.Property(message => message.Role).HasMaxLength(32).IsRequired();
        entity.Property(message => message.Content).IsRequired();
        entity.Property(message => message.CitationsJson).HasColumnType("jsonb");
        entity.HasIndex(message => message.ConversationId);
        entity.HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
