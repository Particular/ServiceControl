namespace Particular.ThroughputCollector;

using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;

static class ServiceCollectionExtensions
{
    static string? registeredPersistence = null;

    public static void AddPersistence(this IServiceCollection services, string persistenceType)
    {
        if (!string.IsNullOrEmpty(registeredPersistence))
        {
            throw new InvalidOperationException("A persistence type has already been registered");
        }

        registeredPersistence = persistenceType;

        AssemblyLoadContext.Default.Resolving += ResolvePersistenceAssembly;

        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(persistenceType);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        var persistence = persistenceConfiguration.Create(persistenceSettings);
        var persistenceService = persistence.Configure(services);

        services.AddSingleton(persistenceService);
        services.AddHostedService(sp => sp.GetRequiredService<PersistenceService>());
    }

    static Assembly? ResolvePersistenceAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
    {
        if (registeredPersistence == null)
        {
            return null;
        }

        var persistenceFolder = PersistenceManifestLibrary.GetPersistenceFolder(registeredPersistence);
        if (persistenceFolder == null)
        {
            return null;
        }

        var path = Path.Combine(persistenceFolder, $"{assemblyName.Name}.dll");

        return File.Exists(path)
            ? loadContext.LoadFromAssemblyPath(path)
            : null;
    }
}
