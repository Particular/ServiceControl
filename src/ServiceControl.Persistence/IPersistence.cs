namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void Configure(IServiceCollection services);
        IPersistenceInstaller CreateInstaller();
    }
}
