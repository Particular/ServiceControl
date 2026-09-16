namespace ServiceControl.Persistence.DataMigration;

public enum MigrationSkipReason
{
    BodyUnreadable,
    // Never written by a copier. A database a newer build wrote still reads rather than throwing where the host decides whether to start.
    Unknown
}
