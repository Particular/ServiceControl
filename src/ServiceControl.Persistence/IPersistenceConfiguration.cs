namespace ServiceControl.Persistence
{
    using Configuration;

    public interface IPersistenceConfiguration
    {
        PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace);
        IPersistence Create(PersistenceSettings settings);
    }
}