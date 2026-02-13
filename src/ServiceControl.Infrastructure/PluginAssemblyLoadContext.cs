namespace ServiceControl.Infrastructure;

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
            // Before loading the assembly from the plugin folder, we should check if the assembly has already been loaded by the default context.
            // This is necessary because we don't have a clean separation of dependencies, so both the instances and the plugins can use the same dependencies.
            // If we let the plugin context load a separate copy, then we run into problems, where the same type in each context are considered different types.
            // Since we ensure we are using the same version of dependencies in every project, it should be okay to use the already loaded copy.
            foreach (var assembly in Default.Assemblies)
            {
                var loadedAssembly = assembly.GetName();

                if (loadedAssembly.Name == assemblyName.Name && loadedAssembly.Version >= assemblyName.Version)
                {
                    return null;
                }
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
