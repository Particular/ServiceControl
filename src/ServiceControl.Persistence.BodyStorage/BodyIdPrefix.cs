namespace ServiceControl.Persistence.BodyStorage;

static class BodyIdPrefix
{
    static readonly char[] Forbidden = ['*', '?', '/', '\\'];

    // A blank prefix would match every body, and wildcard or path characters would widen or escape the match.
    public static void Validate(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (prefix.IndexOfAny(Forbidden) >= 0 || prefix.Contains(".."))
        {
            throw new ArgumentException($"Body id prefix '{prefix}' must not contain wildcard or path characters.", nameof(prefix));
        }
    }
}
