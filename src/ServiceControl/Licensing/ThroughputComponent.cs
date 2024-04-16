
namespace Particular.ServiceControl;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using global::ServiceControl.Infrastructure;
using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Persistence;
using global::ServiceControl.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector.Shared;
using ServiceBus.Management.Infrastructure.Settings;
using ThroughputCollector;
using ThroughputPersistence = ThroughputCollector.Persistence;

class ThroughputComponent : ServiceControlComponent
{
    bool addedDefaultContextAssemblyResolver;
    ThroughputPersistence.PersistenceManifest componentPersistenceManifest;

    public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
    {
        AddPersistence(settings, hostBuilder.Services);
        hostBuilder.AddThroughputCollector(
            TransportManifestLibrary.Find(settings.TransportType)?.Name ?? settings.TransportType,
            settings.ErrorQueue,
            settings.ServiceName,
            LicenseManager.FindLicense().Details.RegisteredTo,
            ServiceControlVersion.GetFileVersion(),
            transportCustomization.ThroughputQueryProvider);
    }

    public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
    {
        context.CreateQueue(PlatformEndpointHelper.ServiceControlThroughputDataQueue);

        AddPersistence(settings, hostBuilder.Services);
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<ThroughputPersistence.IPersistenceInstaller>().Install());
    }

    public void AddPersistence(Settings settings, IServiceCollection services)
    {
        if (!addedDefaultContextAssemblyResolver)
        {
            // Only add the assembly resolver if this is the first time the method has been called
            AssemblyLoadContext.Default.Resolving += ResolvePersistenceAssemblyInDefaultContext;
            addedDefaultContextAssemblyResolver = true;
        }

        var hostPersistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType)
            ?? throw new InvalidOperationException($"No manifest found for {settings.PersistenceType} persistenceType");

        componentPersistenceManifest = ThroughputPersistence.PersistenceManifestLibrary.Find(hostPersistenceManifest.Name)
            ?? throw new InvalidOperationException($"No persistence manifest found for {nameof(ThroughputComponent)}'s {hostPersistenceManifest.Name} persistenceType");

        var loadContext = PersistenceFactory.DetermineLoadContext(settings, hostPersistenceManifest.AssemblyPath);
        if (loadContext is PluginAssemblyLoadContext pluginLoadContext)
        {
            if (!pluginLoadContext.HasResolver(componentPersistenceManifest.AssemblyPath))
            {
                pluginLoadContext.AddResolver(componentPersistenceManifest.AssemblyPath);
            }
        }

        var type = Type.GetType(componentPersistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true)
            ?? throw new InvalidOperationException($"Could not load type '{componentPersistenceManifest.TypeName}' for requested persistence type '{hostPersistenceManifest.Name}' from '{loadContext.Name}' load context");

        if (Activator.CreateInstance(type) is not ThroughputPersistence.IPersistenceConfiguration config)
        {
            throw new InvalidOperationException($"{componentPersistenceManifest.TypeName} does not implement IPersistenceConfiguration");
        }

        services.AddPersistence(config);
    }

    Assembly ResolvePersistenceAssemblyInDefaultContext(AssemblyLoadContext context, AssemblyName name) =>
        File.Exists(componentPersistenceManifest?.AssemblyPath)
            ? context.LoadFromAssemblyPath(componentPersistenceManifest.AssemblyPath)
            : null;
}