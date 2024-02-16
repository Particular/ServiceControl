namespace ServiceControl.Persistence
{
    public interface IPersistenceConfiguration
    {
        PersistenceSettings CreateSettings(string settingsRootNamespace);
        IPersistence Create(PersistenceSettings settings);
    }
}