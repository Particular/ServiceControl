namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Http;
    using Raven.Client.ServerWide.Operations;
    using Sparrow.Json;

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

        class GetLicenseOperation : IServerOperation<LicenseResult>
        {
            public RavenCommand<LicenseResult> GetCommand(DocumentConventions conventions, JsonOperationContext context) => new GetLicenseCommand();
        }

        class GetLicenseCommand : RavenCommand<LicenseResult>
        {
            public override bool IsReadRequest => true;

            public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
            {
                url = $"{node.Url}/license/status";
                return new HttpRequestMessage { Method = HttpMethod.Get };
            }

            public override void SetResponse(JsonOperationContext context, BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                {
                    return;
                }

                if (response.TryGet<string>("Id", out var id) && response.TryGet<string>("LicensedTo", out var licensedTo))
                {
                    Result = new LicenseResult(id, licensedTo);
                }
            }
        }

        record class LicenseResult(string Id, string LicensedTo);
    }
}
