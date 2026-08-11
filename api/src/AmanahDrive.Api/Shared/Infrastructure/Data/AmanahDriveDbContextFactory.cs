using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AmanahDrive.Api.Shared.Infrastructure.Data;

public sealed class AmanahDriveDbContextFactory : IDesignTimeDbContextFactory<AmanahDriveDbContext>
{
    public AmanahDriveDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=amanah_drive;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<AmanahDriveDbContext>()
            .UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new AmanahDriveDbContext(options);
    }
}
