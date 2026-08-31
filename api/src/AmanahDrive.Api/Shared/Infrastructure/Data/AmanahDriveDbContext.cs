using AmanahDrive.Api.Modules.Admin.Models;
using AmanahDrive.Api.Modules.Agent.Models;
using AmanahDrive.Api.Modules.Auth.Models;
using AmanahDrive.Api.Modules.Drive.Models;
using AmanahDrive.Api.Modules.Processing.Models;
using AmanahDrive.Api.Modules.SearchChat.Models;
using Microsoft.EntityFrameworkCore;

namespace AmanahDrive.Api.Shared.Infrastructure.Data;

public sealed class AmanahDriveDbContext(DbContextOptions<AmanahDriveDbContext> options) : DbContext(options)
{
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();

    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Folder> Folders => Set<Folder>();

    public DbSet<FileItem> FileItems => Set<FileItem>();

    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<AgentRunStep> AgentRunSteps => Set<AgentRunStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AmanahDriveDbContext).Assembly);
    }
}
