namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void AddPersistence(IServiceCollection services);
        void AddInstaller(IServiceCollection services);
    }
}