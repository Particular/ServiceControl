namespace ServiceControl.Persistence.DataMigration;

public interface IMigrationSourceFactory
{
    IMigrationSource CreateSource(PersistenceSettings settings);
}
