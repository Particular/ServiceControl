namespace ServiceControl.Infrastructure;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    readonly List<AssemblyDependencyResolver> resolvers = [];
    readonly HashSet<string> resolverPaths = [];

    public PluginAssemblyLoadContext(string assemblyPath) : base(assemblyPath)
    {
        AddResolver(assemblyPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = resolvers
            .Select(resolver => resolver.ResolveAssemblyToPath(assemblyName))
            .FirstOrDefault(path => path is not null);

        if (assemblyPath is not null)
        {
            // Before loading the assembly from the plugin folder, we should check if the assembly has already been loaded by the default context.
            // This is necessary because we don't have a clean separation of dependencies, so both the instances and the plugins can use the same dependencies.
            // If we let the plugin context load a separate copy, then we run into problems, where the same type in each context are considered different types.
            // Since we ensure we are using the same version of dependencies in every project, it should be okay to use the already loaded copy.
            foreach (var assembly in Default.Assemblies)
            {
                if (assembly.FullName == assemblyName.FullName)
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
        var unmanagedDllPath = resolvers
            .Select(resolver => resolver.ResolveUnmanagedDllToPath(unmanagedDllName))
            .FirstOrDefault(path => path is not null);

        if (unmanagedDllPath is not null)
        {
            return LoadUnmanagedDllFromPath(unmanagedDllPath);
        }

        return nint.Zero;
    }

    public bool HasResolver(string path) => resolverPaths.Contains(path);

    public void AddResolver(string path)
    {
        resolvers.Add(new(path));
        resolverPaths.Add(path);
    }
}
