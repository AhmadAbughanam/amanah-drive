using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.SearchChat.Options;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    [Range(1, 25)]
    public int TopK { get; init; } = 5;

    [Range(1, 50)]
    public int ChatHistoryMessageLimit { get; init; } = 10;

    [Range(50, 1000)]
    public int SnippetLength { get; init; } = 300;

    [Range(1, 100)]
    public int RateLimitPermitLimit { get; init; } = 20;

    [Range(1, 60)]
    public int RateLimitWindowMinutes { get; init; } = 1;

    [Range(1, 100)]
    public int ChatDefaultPageSize { get; init; } = 50;

    [Range(1, 500)]
    public int ChatMaxPageSize { get; init; } = 100;
}
