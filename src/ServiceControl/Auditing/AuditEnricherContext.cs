namespace ServiceControl.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Transport;
    using ServiceControl.Operations;

    // Unlike the standalone audit instance there is no ICommand overload. Endpoints detected from audit
    // headers are collected here and written through the monitoring unit of work, the same way the error
    // path does it, rather than sent to the primary's input queue.
    class AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<TransportOperation> outgoingSends, IDictionary<string, object> metadata)
    {
        List<EndpointDetails> newEndpoints;

        public IReadOnlyDictionary<string, string> Headers { get; } = headers;

        public IDictionary<string, object> Metadata { get; } = metadata;

        public IEnumerable<EndpointDetails> NewEndpoints => newEndpoints ?? Enumerable.Empty<EndpointDetails>();

        public void Add(EndpointDetails endpointDetails)
        {
            newEndpoints ??= [];

            newEndpoints.Add(endpointDetails);
        }

        public void AddForSend(TransportOperation transportOperation) => outgoingSends.Add(transportOperation);
    }
}
