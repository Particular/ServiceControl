namespace ServiceControl.Management.PowerShell;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

class InstallerEngineAssemblyLoadContext : AssemblyLoadContext
{
    readonly DependencyResolver resolver;

    public InstallerEngineAssemblyLoadContext()
    {
        var executingAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var assemblyPath = Path.Combine(executingAssemblyDirectory, "InstallerEngine", "ServiceControlInstaller.Engine.dll");

        resolver = new(assemblyPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var unmanagedDllPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (unmanagedDllPath != null)
        {
            return LoadUnmanagedDllFromPath(unmanagedDllPath);
        }

        return IntPtr.Zero;
    }
}