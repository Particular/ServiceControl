namespace ServiceControl.Audit.Persistence
{
    public interface IPersistenceConfiguration
    {
        IPersistence Create(PersistenceSettings settings);
    }
}