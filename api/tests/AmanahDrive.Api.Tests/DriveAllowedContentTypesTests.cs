using AmanahDrive.Api.Modules.Drive.Options;

namespace AmanahDrive.Api.Tests;

public sealed class DriveAllowedContentTypesTests
{
    [Fact]
    public void Defaults_IncludeEveryExtractableContentType()
    {
        var allowedTypes = new DriveOptions().AllowedContentTypes;

        Assert.Contains("application/pdf", allowedTypes);
        Assert.Contains("application/vnd.openxmlformats-officedocument.wordprocessingml.document", allowedTypes);
        Assert.Contains("image/jpeg", allowedTypes);
        Assert.Contains("image/png", allowedTypes);
        Assert.Contains("text/csv", allowedTypes);
        Assert.Contains("text/markdown", allowedTypes);
        Assert.Contains("text/plain", allowedTypes);
    }
}
