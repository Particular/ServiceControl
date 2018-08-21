namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IEnrichImportedMessages
    {
        bool EnrichErrors { get; }
        bool EnrichAudits { get; }
        Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);
    }
}