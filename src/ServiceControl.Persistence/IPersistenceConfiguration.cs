namespace ServiceControl.Persistence
{
    using Configuration;

    public interface IPersistenceConfiguration
    {
        /// <summary>Whether maintenance mode has anything to expose: only RavenDB, which hosts the database in-process.</summary>
        bool SupportsMaintenanceMode { get; }

        PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace);
        IPersistence Create(PersistenceSettings settings);
    }
}