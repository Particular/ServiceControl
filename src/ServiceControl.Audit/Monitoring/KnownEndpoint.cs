namespace ServiceControl.Audit.Monitoring
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

        public static string MakeId(string endpointName, Guid endpointHostId)
        {
            return $"{CollectionName}/{DeterministicGuid.MakeId(endpointName, endpointHostId.ToString())}";
        }

        internal const string CollectionName = "KnownEndpoint";
    }
}