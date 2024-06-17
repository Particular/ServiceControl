namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using NuGet.Versioning;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;
    using Raven.Embedded;

    public static class StartupChecks
    {
        public static async Task EnsureServerVersion(IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);
            var embeddedVersion = SemanticVersion.Parse(typeof(EmbeddedServer).Assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion);
            var serverVersion = SemanticVersion.Parse(build.FullVersion);
            var exception = new Exception(
                $"ServiceControl expects a RavenDB Server with a version {embeddedVersion.Major}.{embeddedVersion.Minor} or higher. The current server version is {build}.");

            if (serverVersion.Major == embeddedVersion.Major)
            {
                if (serverVersion.Minor >= embeddedVersion.Minor)
                {
                    return;
                }

                throw exception;
            }

            if (serverVersion.Major < embeddedVersion.Major)
            {
                throw exception;
            }
        }

        // Must go after the database setup, as database must exist
        public static void EnsureSingleNodeTopology(IDocumentStore store)
        {
            using (var session = store.OpenAsyncSession())
            {
                if (session.Advanced.RequestExecutor.Topology.Nodes.Count > 1)
                {
                    throw new Exception("ServiceControl expects to run against a single-node RavenDB database. Cluster configurations are not supported.");
                }
            }
        }
    }
}
