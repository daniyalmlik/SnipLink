namespace SnipLink.Api.Services;

public interface ISlugGenerator
{
    /// <summary>Generates a cryptographically random alphanumeric slug.</summary>
    string Generate(int length = 7);

    /// <summary>
    /// Returns true when slug is 3–50 chars, lowercase alphanumeric + hyphens,
    /// and does not start or end with a hyphen.
    /// </summary>
    bool IsValid(string slug);
}
