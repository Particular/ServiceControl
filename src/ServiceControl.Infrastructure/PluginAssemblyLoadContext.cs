namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    readonly AssemblyDependencyResolver resolver;

    public PluginAssemblyLoadContext(string assemblyFolder, string typeName) : base(assemblyFolder)
    {
        var parts = typeName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var assemblyName = parts[1];
        var assemblyPath = Path.Combine(assemblyFolder, $"{assemblyName}.dll");

        resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (Default.Assemblies.Any(a => a.FullName == assemblyName.FullName))
        {
            return null;
        }

        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath is not null)
        {
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
