namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void Configure(IServiceCollection services);
        IPersistenceInstaller CreateInstaller();
    }
}