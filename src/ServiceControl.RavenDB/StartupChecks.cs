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
            var serverProductVersion = new Version(build.ProductVersion);

            var clientVersion = typeof(Raven.Client.Constants).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            var parts = clientVersion.Split('.');
            var clientProductVersion = new Version($"{parts[0]}.{parts[1]}");

            if (clientProductVersion > serverProductVersion)
            {
                throw new Exception($"ServiceControl expects RavenDB Server version {clientProductVersion} or higher, but the server is using {serverProductVersion}.");
            }
        }
    }
}
