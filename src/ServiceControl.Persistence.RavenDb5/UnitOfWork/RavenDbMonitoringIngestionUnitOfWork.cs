namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using ServiceControl.Persistence.UnitOfWork;
    using Raven.Client.Documents.Commands.Batches;

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

        static PutCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint) => new PutCommandData
        {
            Document = JObject.FromObject(endpoint),
            Etag = null,
            Key = endpoint.Id.ToString(),
            Metadata = KnownEndpointMetadata
        };

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