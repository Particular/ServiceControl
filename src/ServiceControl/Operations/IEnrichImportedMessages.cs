namespace ServiceControl.Operations
{
    using System.Collections.Generic;

    interface IEnrichImportedMessages
    {
        void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);
        bool EnrichErrors { get; }
        bool EnrichAudits { get; }
    }
}