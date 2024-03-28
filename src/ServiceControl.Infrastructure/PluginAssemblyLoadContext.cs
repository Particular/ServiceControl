﻿namespace ServiceControl.Infrastructure;

using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(assemblyPath)
{
    readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly Load(AssemblyName assemblyName)
    {
        foreach (var assembly in Default.Assemblies)
        {
            if (assembly.FullName == assemblyName.FullName)
            {
                return null;
            }
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
