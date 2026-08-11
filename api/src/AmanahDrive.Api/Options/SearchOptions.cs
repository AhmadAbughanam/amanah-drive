using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Options;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    [Range(1, 25)]
    public int TopK { get; init; } = 5;

    [Range(1, 50)]
    public int ChatHistoryMessageLimit { get; init; } = 10;

    [Range(50, 1000)]
    public int SnippetLength { get; init; } = 300;
}
