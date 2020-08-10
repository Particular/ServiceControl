namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Document;

    interface IBodyStorage
    {
        Task<string> Store(BulkInsertOperation bulkInsert, string bodyId, string contentType, int bodySize, Stream bodyStream);
        Task<StreamResult> TryFetch(IDocumentStore documentStore, string bodyId);
    }

    struct StreamResult
    {
        public bool HasResult;
        public Stream Stream;
        public string ContentType;
        public int BodySize;
        public string Etag;
    }
}