using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Shared.Infrastructure.Cors;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    public string[] AllowedOrigins { get; init; } = ["http://localhost:3000"];
}
