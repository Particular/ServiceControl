namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;

    public static class StartupChecks
    {
        public static async Task EnsureServerVersion(IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);
            var embeddedVersion = typeof(Raven.Embedded.EmbeddedServer).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;

            if (embeddedVersion != build.FullVersion)
            {
                throw new Exception($"ServiceControl expects RavenDB Server version {embeddedVersion} but the server is using {build.FullVersion}.");
            }
        }
    }
}
