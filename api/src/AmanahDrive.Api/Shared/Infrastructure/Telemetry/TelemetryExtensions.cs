using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AmanahDrive.Api.Shared.Infrastructure.Telemetry;

public static class TelemetryExtensions
{
    public static IServiceCollection AddApplicationTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>()
            ?? new TelemetryOptions();

        services.AddOptions<TelemetryOptions>()
            .Bind(configuration.GetSection(TelemetryOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(value => Uri.TryCreate(value.OtlpEndpoint, UriKind.Absolute, out _), "Telemetry:OtlpEndpoint must be an absolute URI.")
            .ValidateOnStart();

        if (!options.TracingEnabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                    instrumentation.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(instrumentation => instrumentation.RecordException = true)
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri(options.OtlpEndpoint);
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                    exporter.TimeoutMilliseconds = 5000;
                }));

        return services;
    }
}
