namespace ServiceControl.Audit.Persistence
{
    public interface IPersistenceConfiguration
    {
        string Name { get; }

        IPersistence Create(PersistenceSettings settings);
    }
}