namespace Particular.LicensingComponent.Contracts;

/// <summary>
/// One key in a usage report's environment data, together with how to read its value.
/// </summary>
/// <remarks>
/// The value is deferred rather than supplied so that reading it can be isolated. A datum that
/// cannot be read costs only its own key, never a sibling's, and providers therefore carry no
/// error handling of their own.
/// </remarks>
public sealed record EnvironmentDatum(string Key, Func<CancellationToken, ValueTask<string>> ReadValue)
{
    /// <summary>
    /// Reported in place of a value whose read threw. Deliberately not a word that could pass for a
    /// state the instance is legitimately in: it always means the read failed, never that the thing
    /// being described is absent or switched off.
    /// </summary>
    public const string ReadFailed = "ReadFailed";

    /// <summary>
    /// A value that is already at hand, such as one read from configuration. Still deferred, so
    /// that nothing is evaluated while a provider is listing what it offers.
    /// </summary>
    public static EnvironmentDatum Value(string key, Func<string> readValue) =>
        new(key, _ => new ValueTask<string>(readValue()));

    /// <summary>
    /// A value that has to be fetched, such as one read from storage or from the database itself.
    /// </summary>
    public static EnvironmentDatum Deferred(string key, Func<CancellationToken, ValueTask<string>> readValue) =>
        new(key, readValue);
}
