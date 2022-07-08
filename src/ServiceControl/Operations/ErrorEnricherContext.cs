namespace ServiceControl.Operations
{
    using System.Collections.Generic;

    class ErrorEnricherContext
    {
        public ErrorEnricherContext(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
        {
            Headers = headers;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, object> Metadata { get; }
    }
}