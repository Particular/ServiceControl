#nullable enable

namespace ServiceControl.Infrastructure;

using System.IO;
using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(assemblyPath)
{
    readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var pluginPath = resolver.ResolveAssemblyToPath(assemblyName);
        if (pluginPath is null)
        {
            // The plugin did not ship this dependency at all. Let the runtime fall through to the default ALC's own resolver
            // (TPA / shared framework / probing), which is the correct behavior for a host-only dependency.
            return null;
        }

        // Tier 1: ask the default ALC to resolve a fresh copy. This is the path that hits the host's deps.json TPA, which is
        // how a plugin can see NServiceBus.CustomChecks as the same Type instance the host's DI container holds. If the
        // default ALC has no candidate for this name (FileNotFound / FileLoad), we fall through to tier 2.
        try
        {
            var fromDefault = Default.LoadFromAssemblyName(assemblyName);
            if (fromDefault.GetName().Version >= assemblyName.Version)
            {
                return fromDefault;
            }
        }
        catch (FileNotFoundException)
        {
            // Default ALC has no candidate; fall through.
        }
        catch (FileLoadException)
        {
            // Default ALC has a candidate that failed to load (e.g., a bad image); fall through to the plugin's copy.
        }

        // Tier 2: the plugin's local copy. Better to load a plugin-shipped version than to fail the load entirely.
        return LoadFromAssemblyPath(pluginPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var unmanagedDllPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        return unmanagedDllPath is not null ? LoadUnmanagedDllFromPath(unmanagedDllPath) : nint.Zero;
    }
}