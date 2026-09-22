namespace ServiceBus.Management.Infrastructure.Settings
{
    /// <summary>
    /// Where a primary's audit data lives. Local is the shared database, the default wherever the
    /// persister supports audit. Remote means a dedicated audit database served by an audit host
    /// listed under RemoteInstances, so this primary neither ingests audit nor queries its own audit
    /// tables. Not derived from the other settings, because the shared topology where only workers
    /// ingest looks identical to Remote from the primary's side.
    /// </summary>
    public enum AuditDataLocation
    {
        Local,
        Remote
    }
}
