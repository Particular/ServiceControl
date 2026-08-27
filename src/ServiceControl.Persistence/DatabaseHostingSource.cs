namespace ServiceControl.Persistence;

/// <summary>
/// Where a reported database hosting classification came from, so that a guess can be told apart
/// from an answer when the reports are analysed.
/// </summary>
public static class DatabaseHostingSource
{
    /// <summary>The server itself said so, and is therefore authoritative.</summary>
    public const string Probe = "Probe";

    /// <summary>The configuration says so outright, with nothing left to infer.</summary>
    public const string Configuration = "Configuration";

    /// <summary>Inferred from the configured host name because the server could not be asked.</summary>
    public const string ConnectionString = "ConnectionString";

    /// <summary>Nothing was available to classify.</summary>
    public const string None = "None";
}
