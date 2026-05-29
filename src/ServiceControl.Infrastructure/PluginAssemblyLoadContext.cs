namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(assemblyPath)
{
    readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            // Before loading the assembly from the plugin folder, we should give the default context a chance to resolve shared dependencies.
            // This is necessary because we don't have a clean separation of dependencies, so both the instances and the plugins can use the same dependencies.
            // If we let the plugin context load a separate copy, then we run into problems, where the same type in each context are considered different types.
            // Since we ensure we are using the same version of dependencies in every project, it should be okay to use the default context copy.
            foreach (var assembly in Default.Assemblies)
            {
                var loadedAssembly = assembly.GetName();

                if (loadedAssembly.Name == assemblyName.Name)
                {
                    return loadedAssembly.Version >= assemblyName.Version ? assembly : LoadFromAssemblyPath(assemblyPath);
                }
            }

            try
            {
                var defaultAssembly = Default.LoadFromAssemblyName(assemblyName);
                if (defaultAssembly.GetName().Version >= assemblyName.Version)
                {
                    return defaultAssembly;
                }
            }
            catch (Exception exception) when (exception is FileNotFoundException or FileLoadException)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var unmanagedDllPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (unmanagedDllPath is not null)
        {
            return LoadUnmanagedDllFromPath(unmanagedDllPath);
        }

        return nint.Zero;
    }
}
