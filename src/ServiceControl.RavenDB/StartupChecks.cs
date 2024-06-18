﻿namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;
    using Raven.Embedded;

    public static class StartupChecks
    {
        public static async Task EnsureServerVersion(IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);
            var embeddedVersion = typeof(EmbeddedServer).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            if (embeddedVersion != build.FullVersion)
            {
                throw new Exception($"ServiceControl expects RavenDB Server version {embeddedVersion} but the server is using {build.FullVersion}.");
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