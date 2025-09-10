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
            // RavenDB compatibility policy is that the major/minor version of the server must be
            // equal or higher than the client and ignores the patch version.
            //
            // https://docs.ravendb.net/6.2/client-api/faq/backward-compatibility/#compatibility---ravendb-42-and-higher
            //
            // > Starting with version 4.2, RavenDB clients are compatible with any server of their own version and higher.
            // > E.g. -
            // >
            // > Client 4.2 is compatible with Server 4.2, Server 4.5, Server 5.2, and any other server of a higher version.

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
