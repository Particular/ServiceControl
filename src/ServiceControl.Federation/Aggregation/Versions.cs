namespace ServiceControl.Federation.Aggregation;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// ServicePulse gates features on <c>X-Particular-Version</c>. The proxy reports the lowest member
/// version so it never enables a feature that one member lacks.
/// </summary>
public static class Versions
{
    public static string? Lowest(IEnumerable<string?> versions) =>
        versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .OrderBy(Parse)
            .ThenBy(v => v, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Reads the numeric <c>major.minor.patch</c> prefix, ignoring pre-release and build metadata.
    /// </summary>
    public static Version Parse(string version)
    {
        var numeric = version.Split('-', '+')[0];
        var parts = numeric.Split('.')
            .Select(p => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Concat([0, 0, 0])
            .Take(3)
            .ToArray();
        return new Version(parts[0], parts[1], parts[2]);
    }
}
