namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Reflection;

/// <summary>
/// The document that records which ServiceControl build last wrote a RavenDB database. A migration reads it to
/// decide whether this build reads those documents the same way, which nothing else in the database says.
/// </summary>
public class RavenDataVersion
{
    public const string DocumentId = "ServiceControl/DataVersion";

    // The ServiceControl release version, put on this assembly by MinVer. The "+hash" suffix comes off so that a
    // rebuild of the same release does not read as a new data version.
    public static string Current { get; } =
        typeof(RavenDataVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(RavenDataVersion).Assembly.GetName().Version!.ToString(3);

    /// <summary>The release that stamped the database. It can be null on a document written before this property existed, because RavenDB deserializes without honoring the required modifier.</summary>
    public required string Version { get; set; }

    /// <summary>When the stamp was last raised, in UTC.</summary>
    public DateTime StampedAt { get; set; }
}
