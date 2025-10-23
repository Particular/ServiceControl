namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.Configuration;

    public interface IPersistenceConfiguration
    {
        string Name { get; }

        IPersistence Create(PersistenceSettings settings, IConfiguration configuration);
    }
}