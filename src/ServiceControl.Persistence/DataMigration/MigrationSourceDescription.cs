namespace ServiceControl.Persistence.DataMigration;

public sealed record MigrationSourceDescription(
    bool Embedded,
    string ServerUrl,
    string PrimaryDatabase,
    string ThroughputDatabase,
    string ServerVersion);
