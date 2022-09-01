namespace ServiceControl.Audit.Persistence.Monitoring
{
    using System;
    using Infrastructure;

    public class KnownEndpoint
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }

        public DateTime LastSeen { get; set; }

        //TODO: move to ravendb projects
        public static string MakeDocumentId(string endpointName, Guid endpointHostId)
        {
            return $"{CollectionName}/{DeterministicGuid.MakeId(endpointName, endpointHostId.ToString())}";
        }

        public const string CollectionName = "KnownEndpoints";
    }
}