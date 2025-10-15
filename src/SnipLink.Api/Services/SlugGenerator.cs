using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SnipLink.Api.Services;

public sealed class SlugGenerator : ISlugGenerator
{
    private const string Charset = "abcdefghijklmnopqrstuvwxyz0123456789";
    // 252 = largest multiple of 36 that fits in a byte (7 × 36).
    // Bytes >= 252 are rejected to eliminate modulo bias.
    private const int MaxUnbiased = 252;

    // Pre-compiled: lowercase alphanumeric + hyphens, 2-50 chars,
    // no leading/trailing hyphen, no consecutive hyphens.
    private static readonly Regex ValidSlugRegex =
        new(@"^[a-z0-9]([a-z0-9\-]{0,48}[a-z0-9]|[a-z0-9]?)$",
            RegexOptions.Compiled);

    public string Generate(int length = 7)
    {
        if (length < 3 || length > 50)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be 3–50.");

        var result = new char[length];
        var buffer = new byte[length];

        // Rejection-sampling loop: statistically completes in < 2 iterations.
        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            bool clean = true;
            for (int i = 0; i < length; i++)
            {
                if (buffer[i] >= MaxUnbiased) { clean = false; break; }
                result[i] = Charset[buffer[i] % Charset.Length];
            }
            if (clean) return new string(result);
        }
    }

    public bool IsValid(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return false;
        if (slug.Length < 2 || slug.Length > 50) return false;
        return ValidSlugRegex.IsMatch(slug);
    }
}
