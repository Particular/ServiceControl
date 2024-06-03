#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Http;
    using Raven.Client.ServerWide.Operations;
    using Sparrow.Json;

    sealed class RavenExternalPersistenceLifecycle(DatabaseConfiguration configuration) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
    {
        public async ValueTask<IDocumentStore> GetDocumentStore(CancellationToken cancellationToken = default)
        {
            if (documentStore != null)
            {
                return documentStore;
            }

            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);
                return documentStore ?? throw new InvalidOperationException("Document store is not available. Ensure `IRavenPersistenceLifecycle.Initialize` is invoked");
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);

                var store = new DocumentStore
                {
                    Database = configuration.Name,
                    Urls = [configuration.ServerConfiguration.ConnectionString],
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (configuration.FindClrType != null)
                {
                    store.Conventions.FindClrType += configuration.FindClrType;
                }

                documentStore = store.Initialize();

                var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);
                var embeddedVersion = typeof(Raven.Embedded.EmbeddedServer).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
                if (embeddedVersion != build.FullVersion)
                {
                    throw new Exception($"ServiceControl expects RavenDB Server version {embeddedVersion} but the server is using {build.FullVersion}.");
                }

                var licenseResponse = await store.Maintenance.Server.SendAsync(new GetLicenseOperation(), cancellationToken);
                if (licenseResponse.LicensedTo != "ParticularNservicebus (Israel)")
                {
                    throw new Exception("Wrong license");
                }

                var databaseSetup = new DatabaseSetup(configuration);
                await databaseSetup.Execute(store, cancellationToken);

                // Must go after the database setup, as database must exist
                using (var session = documentStore.OpenAsyncSession())
                {
                    if (session.Advanced.RequestExecutor.Topology.Nodes.Count > 1)
                    {
                        throw new Exception("ServiceControl expects to run against a single-node RavenDB database. Cluster configurations are not supported.");
                    }
                }
            }
            finally
            {
                initializeSemaphore.Release();
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

        public void Dispose() => documentStore?.Dispose();

        IDocumentStore? documentStore;
        readonly SemaphoreSlim initializeSemaphore = new(1, 1);
    }
}