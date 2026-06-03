#nullable enable

namespace ServiceControl.Infrastructure;

using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(assemblyPath)
{
    readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath is null)
        {
            // The requested assembly is not a dependency of the plugin assembly, so it was not found in the plugin folder.
            // Return null to let the default context handle it.
            return null;
        }

        // Check the default context first to see if the requested assembly is available.
        // If it is, we want to use it to ensure proper dependency sharing.
        try
        {
            var defaultAssembly = Default.LoadFromAssemblyName(assemblyName);

            if (defaultAssembly.GetName().Version >= assemblyName.Version)
            {
                return defaultAssembly;
            }
        }
        catch { }

        // Load the requested assembly from the plugin folder
        return LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var unmanagedDllPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        return unmanagedDllPath is not null ? LoadUnmanagedDllFromPath(unmanagedDllPath) : nint.Zero;
    }
}