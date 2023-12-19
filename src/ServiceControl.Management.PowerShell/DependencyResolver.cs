﻿#nullable enable
namespace ServiceControl.Management.PowerShell;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyModel;

class DependencyResolver
{
    readonly string assemblyDirectory;
    readonly DependencyContext dependencyContext;
    readonly string?[] runtimes;

    public DependencyResolver(string assemblyPath)
    {
        assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? string.Empty;

        var depsJsonFile = Path.ChangeExtension(assemblyPath, "deps.json");
        using var fileStream = File.OpenRead(depsJsonFile);

        var reader = new DependencyContextJsonReader();
        dependencyContext = reader.Read(fileStream);

        var runtimeGraph = DependencyContext.Default?.RuntimeGraph.SingleOrDefault(r => r.Runtime.Equals(RuntimeInformation.RuntimeIdentifier, StringComparison.Ordinal));

        // PowerShell is still building against OS-specific RIDs on Windows, so the expected runtime graph information isn't in the default deps.json file.
        // Try looking it up again using the RID specified in the deps.json file instead.
        runtimeGraph ??= DependencyContext.Default?.RuntimeGraph.SingleOrDefault(r => r.Runtime.Equals(DependencyContext.Default.Target.Runtime, StringComparison.Ordinal));

        if (runtimeGraph is not null)
        {
            runtimes = [runtimeGraph.Runtime, .. runtimeGraph.Fallbacks, string.Empty];
        }
        else // Runtime graph information isn't available when running in WinRM, so assume we're running on Windows if null at this point
        {
            runtimes = [Environment.Is64BitProcess ? "win-x64" : "win-x86", "win", "any", "base", string.Empty];
        }
    }

    public string? ResolveAssemblyToPath(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        var library = dependencyContext.RuntimeLibraries.SingleOrDefault(r => r.Name.Equals(assemblyName.Name, StringComparison.Ordinal));

        if (library is null)
        {
            return null;
        }

        // If we had dependencies that had satellite resource assemblies, we'd need to use assemblyName.CultureName and library.ResourceAssemblies instead

        return SearchRuntimeAssets(library.RuntimeAssemblyGroups);
    }

    public string? ResolveUnmanagedDllToPath(string unmanagedDllName)
    {
        //This logic is good enough for our purposes, but is not comprehensive enough for general, cross-platform use.
        var nativeLibraryGroups = dependencyContext.RuntimeLibraries.SelectMany(r => r.NativeLibraryGroups);
        var candidateGroups = nativeLibraryGroups.Where(r => r.AssetPaths[0].Contains(unmanagedDllName, StringComparison.OrdinalIgnoreCase));

        return SearchRuntimeAssets(candidateGroups);
    }

    string? SearchRuntimeAssets(IEnumerable<RuntimeAssetGroup> runtimeAssets)
    {
        if (!runtimeAssets.Any())
        {
            return null;
        }

        string? assetPath = null;

        foreach (var runtime in runtimes)
        {
            foreach (var asset in runtimeAssets)
            {
                if (asset.Runtime.Equals(runtime, StringComparison.Ordinal))
                {
                    assetPath = asset.AssetPaths[0];
                    break;
                }
            }

            if (assetPath is not null)
            {
                // This assumes that we're running with all assemblies copied locally, and doesn't attempt to cover the scenario of needing to resolve
                // from the NuGet package cache folder.
                if (assetPath.StartsWith("lib", StringComparison.Ordinal))
                {
                    assetPath = Path.GetFileName(assetPath);
                }

                assetPath = Path.GetFullPath(Path.Combine(assemblyDirectory, assetPath));

                if (!File.Exists(assetPath))
                {
                    assetPath = null;
                }

                break;
            }
        }

        return assetPath;
    }
}
