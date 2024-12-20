﻿namespace ServiceControl.RavenDB
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

            var clientVersion = typeof(Raven.Client.Constants).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            var parts = clientVersion.Split('.');
            var clientProductVersion = $"{parts[0]}.{parts[1]}";

            if (clientProductVersion != build.ProductVersion)
            {
                throw new Exception($"ServiceControl expects RavenDB Server version {clientProductVersion} but the server is using {build.ProductVersion}.");
            }
        }
    }
}
