namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        RavenIngestionUnitOfWork parentUnitOfWork;

        public RavenMonitoringIngestionUnitOfWork(RavenIngestionUnitOfWork parentUnitOfWork)
        {
            this.parentUnitOfWork = parentUnitOfWork;
        }

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            parentUnitOfWork.AddCommand(CreateKnownEndpointsPutCommand(knownEndpoint));
            return Task.CompletedTask;
        }

        static PatchCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint)
        {
            var document = JObject.FromObject(endpoint);
            document["@metadata"] = KnownEndpointMetadata;

            var docId = RavenMonitoringDataStore.MakeDocumentId(endpoint.EndpointDetails.GetDeterministicId());

            // Ingestion always observes endpoints with Monitored = false, so patching an
            // already-known endpoint must not stomp a user-set Monitored = true flag back to false.
            var existingDocPatch = new PatchRequest
            {
                Script = @$"
                    var insert = {document};

                    for(var key in insert) {{
                        if(insert.hasOwnProperty(key) && key !== '{nameof(KnownEndpoint.Monitored)}') {{
                            this[key] = insert[key];
                        }}
                    }}"
            };

            // A newly discovered endpoint has no existing Monitored value to preserve, so it
            // still gets the full document, including Monitored = false.
            var patchIfMissing = new PatchRequest
            {
                Script = @$"
                    var insert = {document};

                    for(var key in insert) {{
                        if(insert.hasOwnProperty(key)) {{
                            this[key] = insert[key];
                        }}
                    }}"
            };

            return new PatchCommandData(docId, null, existingDocPatch, patchIfMissing);
        }

        static RavenMonitoringIngestionUnitOfWork()
        {
            KnownEndpointMetadata = JObject.Parse($@"
                                    {{
                                        ""@collection"": ""{RavenMonitoringDataStore.KnownEndpointsCollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        static readonly JObject KnownEndpointMetadata;
    }
}