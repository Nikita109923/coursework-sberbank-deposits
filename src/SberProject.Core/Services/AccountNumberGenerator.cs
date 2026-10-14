using System.Globalization;

namespace SberProject.Core.Services;

public sealed class AccountNumberGenerator
{
    public const string Prefix = "42301933";
    public const int Length = 20;

    public string Next(IEnumerable<string> existingNumbers)
    {
        var lastSequence = existingNumbers
            .Where(n => n.Length == Length && n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => long.TryParse(n.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Prefix + (lastSequence + 1).ToString("D12", CultureInfo.InvariantCulture);
    }
}
