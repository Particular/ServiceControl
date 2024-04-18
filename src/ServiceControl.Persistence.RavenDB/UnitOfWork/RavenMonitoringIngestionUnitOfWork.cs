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

            var request = new PatchRequest
            {
                Script = @$"
                    var insert = {document};

                    for(var key in insert) {{
                        if(insert.hasOwnProperty(key)) {{
                            this[key] = insert[key];
                        }}
                    }}"
            };

            return new PatchCommandData(docId, null, request, request);
        }

        static RavenMonitoringIngestionUnitOfWork()
        {
            KnownEndpointMetadata = JObject.Parse($@"
                                    {{
                                        ""@collection"": ""{KnownEndpoint.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        static readonly JObject KnownEndpointMetadata;
    }
}