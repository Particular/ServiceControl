namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        IPersistenceLifecycle CreateLifecycle(IServiceCollection serviceCollection);
        IPersistenceInstaller CreateInstaller();
    }
}