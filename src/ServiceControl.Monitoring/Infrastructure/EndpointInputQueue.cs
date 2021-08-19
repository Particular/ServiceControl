namespace ServiceControl.Monitoring.Infrastructure
{
    using System;

    // it's not entirely clear whether we can rename this type or not, given that it seems to be used for storage
    // all references seem to indicate it should be called something like QueueId
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public readonly struct EndpointInputQueue : IEquatable<EndpointInputQueue>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        public EndpointInputQueue(string endpointName, string inputQueue)
        {
            EndpointName = endpointName;
            InputQueue = inputQueue;
        }

        public string EndpointName { get; }
        public string InputQueue { get; }

        public bool Equals(EndpointInputQueue other) => string.Equals(EndpointName, other.EndpointName) && string.Equals(InputQueue, other.InputQueue);

        public override bool Equals(object obj) => obj is EndpointInputQueue inputQueue && Equals(inputQueue);

        public override int GetHashCode() => (EndpointName, InputQueue).GetHashCode();
    }
}