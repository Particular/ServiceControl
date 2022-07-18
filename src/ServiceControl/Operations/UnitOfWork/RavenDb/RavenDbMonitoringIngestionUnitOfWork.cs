namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using Monitoring;
    using Raven.Abstractions.Commands;
    using Raven.Json.Linq;

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
            Document = RavenJObject.FromObject(endpoint),
            Etag = null,
            Key = endpoint.Id.ToString(),
            Metadata = KnownEndpointMetadata
        };

        static RavenDbMonitoringIngestionUnitOfWork()
        {
            KnownEndpointMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{KnownEndpoint.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        static readonly RavenJObject KnownEndpointMetadata;
    }
}