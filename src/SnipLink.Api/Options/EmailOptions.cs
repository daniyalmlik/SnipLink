namespace SnipLink.Api.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From     { get; init; } = string.Empty;
    public string FromName { get; init; } = "SnipLink";
    public string Host     { get; init; } = string.Empty;
    public int    Port     { get; init; } = 587;
    public bool   UseSsl   { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
