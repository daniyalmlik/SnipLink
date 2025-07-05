namespace SnipLink.Api.Options;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";
    public string ApiKey { get; init; } = string.Empty;
}
