namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void AddPersistence(IServiceCollection services, IConfiguration configuration);
        void AddInstaller(IServiceCollection services);
    }
}
