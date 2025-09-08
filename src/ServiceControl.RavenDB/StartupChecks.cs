namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using NuGet.Versioning;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;

    public static class StartupChecks
    {
        public static async Task EnsureServerVersion(IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

            var clientVersion = SemanticVersion.Parse(typeof(Raven.Client.Constants).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion);
            var serverVersion = NuGetVersion.Parse(build.ProductVersion);

            var serverHasLowerMajorMinor = serverVersion.Major < clientVersion.Major
                                || (serverVersion.Major == clientVersion.Major && serverVersion.Minor < clientVersion.Minor);

            if (serverHasLowerMajorMinor)
            {
                throw new Exception($"ServiceControl expects at minimum RavenDB Server version {clientVersion.Major}.{clientVersion.Minor} but the server is using {build.ProductVersion}.");
            }
        }
    }
}