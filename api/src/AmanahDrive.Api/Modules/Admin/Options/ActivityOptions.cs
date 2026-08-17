using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.Admin.Options;

public sealed class ActivityOptions
{
    public const string SectionName = "AdminActivity";

    [Range(1, 200)]
    public int DefaultPageSize { get; init; } = 25;

    [Range(1, 200)]
    public int MaxPageSize { get; init; } = 100;
}
