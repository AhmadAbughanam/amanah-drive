using AmanahDrive.Api.Modules.SearchChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.SearchChat.Data;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> entity)
    {
        entity.ToTable("conversations");
        entity.HasKey(conversation => conversation.Id);
        entity.HasIndex(conversation => conversation.UserId);
        entity.HasOne(conversation => conversation.User)
            .WithMany(user => user.Conversations)
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
