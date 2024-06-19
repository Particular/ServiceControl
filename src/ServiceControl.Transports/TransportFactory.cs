namespace ServiceControl.Transports
{
    using System;
    using System.IO;

    public static class TransportFactory
    {
        public static ITransportCustomization Create(TransportSettings settings)
        {
            try
            {
                var transportManifest = TransportManifestLibrary.Find(settings.TransportType);
                var assemblyPath = Path.Combine(transportManifest.Location, $"{transportManifest.AssemblyName}.dll");
                var loadContext = settings.AssemblyLoadContextResolver(assemblyPath);
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