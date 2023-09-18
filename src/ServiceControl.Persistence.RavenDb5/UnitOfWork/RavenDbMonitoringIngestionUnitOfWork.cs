namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using ServiceControl.Persistence.UnitOfWork;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;

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

            return new PatchCommandData(endpoint.Id.ToString(), null, new PatchRequest
            {
                //TODO: check if this works
                Script = $"put('{KnownEndpoint.CollectionName}/', {document}"
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