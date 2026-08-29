namespace AmanahDrive.Api.Modules.Admin.Options;

public sealed class AiPricingOptions
{
    public const string SectionName = "AiPricing";

    public List<AiModelPrice> Models { get; init; } = [];
}

public sealed class AiModelPrice
{
    public bool Enabled { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public decimal InputUsdPerMillionTokens { get; init; }

    public decimal OutputUsdPerMillionTokens { get; init; }
}
