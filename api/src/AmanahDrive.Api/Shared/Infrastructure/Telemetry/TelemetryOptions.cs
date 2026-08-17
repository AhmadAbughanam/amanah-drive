using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Shared.Infrastructure.Telemetry;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool TracingEnabled { get; init; }

    [Required]
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";

    [Required]
    public string ServiceName { get; init; } = "amanah-drive-api";
}
