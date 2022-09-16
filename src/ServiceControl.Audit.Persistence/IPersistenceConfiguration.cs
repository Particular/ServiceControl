namespace ServiceControl.Audit.Persistence
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistenceConfiguration
    {
        void ConfigureServices(IServiceCollection serviceCollection, IDictionary<string, string> settings, bool maintenanceMode, bool isSetup);
    }
}