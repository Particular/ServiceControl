namespace Particular.ThroughputCollector;

using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;

static class ServiceCollectionExtensions
{
    static readonly HashSet<string> PersistenceTypes = [];

    /// <remarks>
    /// It is possible for multiple different hosts to be created by Service Control and its associated test infrastructure,
    /// which means AddPersistence can be called multiple times and potentially with different persistence types
    /// </remarks>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string persistenceType, string persistenceAssembly)
    {
        if (PersistenceTypes.Count == 0)
        {
            // Only add the assembly resolver if this is the first time the method has been called
            AssemblyLoadContext.Default.Resolving += ResolvePersistenceAssembly;
        }

        PersistenceTypes.Add(persistenceType);

        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(persistenceType, persistenceAssembly);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        services.AddSingleton(persistenceSettings);

        var persistence = persistenceConfiguration.Create(persistenceSettings);
        persistence.Configure(services);
        services.AddSingleton(persistence);

        return services;
    }

    static Assembly? ResolvePersistenceAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
    {
        if (loadContext.Name != "Default")
        {
            return null;
        }

        foreach (var persistenceType in PersistenceTypes)
        {
            var manifest = PersistenceManifestLibrary.Find(persistenceType);
            if (manifest?.AssemblyPath is not null && File.Exists(manifest.AssemblyPath))
            {
                return loadContext.LoadFromAssemblyPath(manifest.AssemblyPath);
            }
        }

        return null;
    }
}
