namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.Configuration;

    public interface IPersistenceConfiguration
    {
        IPersistence Create(IConfiguration configuration);
    }
}