namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;
    using System.Reflection;
    using System.Runtime.Loader;

    public class ModuleAssemblyInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        static readonly InstallerEngineAssemblyLoadContext installerEngineLoadContext = new();

        public void OnImport() => AssemblyLoadContext.Default.Resolving += Resolve;

        public void OnRemove(PSModuleInfo psModuleInfo) => AssemblyLoadContext.Default.Resolving -= Resolve;

        static Assembly Resolve(AssemblyLoadContext defaultLoadContext, AssemblyName assemblyName)
        {
            // Don't try to use InstallerEngineAssemblyLoadContext to resolve the assembly it has a dependency on
            if (assemblyName.Name.Contains("Microsoft.Extensions.DependencyModel"))
            {
                return null;
            }
            else
            {
                return installerEngineLoadContext.LoadFromAssemblyName(assemblyName);
            }
        }
    }
}