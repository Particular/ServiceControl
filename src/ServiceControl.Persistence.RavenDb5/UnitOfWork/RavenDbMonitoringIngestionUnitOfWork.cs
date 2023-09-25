namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        RavenDbIngestionUnitOfWork parentUnitOfWork;

        public RavenDbMonitoringIngestionUnitOfWork(RavenDbIngestionUnitOfWork parentUnitOfWork)
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

            var docId = RavenDbMonitoringDataStore.MakeDocumentId(endpoint.EndpointDetails.GetDeterministicId());

            return new PatchCommandData(docId, null, new PatchRequest
            {
                //TODO: check if this works
                Script = $"put('{KnownEndpoint.CollectionName}/', {document})"
            });
        }

        static RavenDbMonitoringIngestionUnitOfWork()
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