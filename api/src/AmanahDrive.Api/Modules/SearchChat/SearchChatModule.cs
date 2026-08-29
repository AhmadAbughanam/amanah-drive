using System.Threading.RateLimiting;
using AmanahDrive.Api.Modules.SearchChat.Endpoints;
using AmanahDrive.Api.Modules.SearchChat.Options;
using AmanahDrive.Api.Modules.SearchChat.Search;
using AmanahDrive.Api.Shared.Infrastructure.Security;

namespace AmanahDrive.Api.Modules.SearchChat;

public static class SearchChatModule
{
    public static IServiceCollection AddSearchChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.ChatDefaultPageSize <= options.ChatMaxPageSize, "Search:ChatDefaultPageSize must be less than or equal to Search:ChatMaxPageSize.")
            .ValidateOnStart();

        var searchOptions = configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>() ?? new SearchOptions();

        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = SecurityRateLimitLogging.LogRejectedAsync;
            options.AddPolicy("ai", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = searchOptions.RateLimitPermitLimit,
                        Window = TimeSpan.FromMinutes(searchOptions.RateLimitWindowMinutes),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapSearchChatModule(this IEndpointRouteBuilder app)
    {
        app.MapSearchChatEndpoints();
        return app;
    }
}
