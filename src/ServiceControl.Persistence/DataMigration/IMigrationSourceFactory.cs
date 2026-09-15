namespace ServiceControl.Persistence.DataMigration;

using ServiceControl.Configuration;

/// <summary>Implemented by a persister that can be read as the old database a migration copies from.</summary>
public interface IMigrationSourceFactory
{
    /// <summary>Builds the source from the instance's own settings, without connecting to it.</summary>
    IMigrationSource CreateSource(SettingsRootNamespace settingsRoot);
}
