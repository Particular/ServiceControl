namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Frozen;
using System.Collections.Generic;

/// <summary>
/// The six custom checks the RavenDB persister runs about its own database, each as its id and the category it reports under.
/// The custom checks reader flags a result only when both match, so a result that reuses one of these ids under another category is not flagged, such as the audit instance's "RavenDB dirty memory" under "ServiceControl.Audit Health".
/// </summary>
static class RavenStorageCheckIds
{
    // Each check passes its id and category to the CustomCheck base constructor as literals, so there is nothing to reference. RavenStorageCheckIdsTests fails if the two lists differ.
    public static IReadOnlySet<(string Id, string Category)> All { get; } = new[]
    {
        ("Error Database Index Errors", "ServiceControl Health"),
        ("Error Database Index Lag", "ServiceControl Health"),
        ("ServiceControl database", "Storage space"),
        ("Message Ingestion Process", "ServiceControl Health"),
        ("RavenDB dirty memory", "ServiceControl Health"),
        ("Error Database Search Engine", "ServiceControl Health")
    }.ToFrozenSet();
}
