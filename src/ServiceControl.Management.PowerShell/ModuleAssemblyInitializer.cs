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

        static Assembly Resolve(AssemblyLoadContext defaultLoadContext, AssemblyName assemblyName) => installerEngineLoadContext.LoadFromAssemblyName(assemblyName);
    }
}
