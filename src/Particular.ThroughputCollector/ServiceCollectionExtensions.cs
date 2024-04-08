﻿namespace Particular.ThroughputCollector;

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
    public static IServiceCollection AddPersistence(this IServiceCollection services, string persistenceType)
    {
        if (PersistenceTypes.Count == 0)
        {
            // Only add the assembly resolver if this is the first time the method has been called
            AssemblyLoadContext.Default.Resolving += ResolvePersistenceAssembly;
        }

        PersistenceTypes.Add(persistenceType);

        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(persistenceType);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        services.AddSingleton(persistenceSettings);

        var persistence = persistenceConfiguration.Create(persistenceSettings);

        if (!services.IsServiceRegistered(persistence.GetType()))
        {
            persistence.Configure(services);
            services.AddSingleton(persistence);
        }

        return services;
    }

    public static bool IsServiceRegistered(this IServiceCollection services, Type serviceType) => services.Any(serviceDescriptor => serviceDescriptor.ServiceType == serviceType);

    static Assembly? ResolvePersistenceAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
    {
        foreach (var persistenceType in PersistenceTypes)
        {
            var manifest = PersistenceManifestLibrary.Find(persistenceType);
            if (manifest?.Location == null)
            {
                continue;
            }

            var path = Path.Combine(manifest.Location, $"{assemblyName.Name}.dll");

            if (File.Exists(path))
            {
                return loadContext.LoadFromAssemblyPath(path);
            }
        }

        return null;
    }
}
