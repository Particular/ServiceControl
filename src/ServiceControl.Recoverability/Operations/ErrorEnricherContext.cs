namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;

    public class ErrorEnricherContext
    {
        List<EndpointDetails> newEndpoints;

        public ErrorEnricherContext(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
        {
            Headers = headers;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, object> Metadata { get; }

        public IEnumerable<EndpointDetails> NewEndpoints => newEndpoints ?? Enumerable.Empty<EndpointDetails>();

        public void Add(EndpointDetails endpointDetails)
        {
            if (newEndpoints == null)
            {
                newEndpoints = new List<EndpointDetails>();
            }

            newEndpoints.Add(endpointDetails);
        }
    }
}