using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AmanahDrive.Api.Data;

public sealed class AmanahDriveDbContextFactory : IDesignTimeDbContextFactory<AmanahDriveDbContext>
{
    public AmanahDriveDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=amanah_drive;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AmanahDriveDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AmanahDriveDbContext(options);
    }
}
