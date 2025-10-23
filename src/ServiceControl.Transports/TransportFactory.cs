namespace ServiceControl.Transports
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using Infrastructure;

    public static class TransportFactory
    {
        internal static Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; } = static (assemblyPath) => new PluginAssemblyLoadContext(assemblyPath);
        public static ITransportCustomization Create(TransportSettings settings)
        {
            try
            {
                var transportManifest = TransportManifestLibrary.Find(settings.TransportType);
                var assemblyPath = Path.Combine(transportManifest.Location, $"{transportManifest.AssemblyName}.dll");
                var loadContext = AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(transportManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (ITransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {settings.TransportType}.", e);
            }
        }
    }
}