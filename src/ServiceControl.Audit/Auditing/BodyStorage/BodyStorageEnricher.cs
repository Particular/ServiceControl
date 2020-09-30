namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using Infrastructure;
    using Infrastructure.Settings;
    using Microsoft.IO;
    using NServiceBus;

    public class BodyStorageEnricher
    {
        public BodyStorageEnricher(Settings settings)
        {
            this.settings = settings;
        }

        public void StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, ProcessedMessage processedMessage, IDictionary<string, string> searchTerms)
        {
            var bodySize = body?.Length ?? 0;
            processedMessage.BodySize = bodySize;
            if (bodySize == 0)
            {
                return;
            }

            var bodyId = headers.ProcessingId();
            var contentType = GetContentType(headers);

            processedMessage.BodyUrl = $"/messages/{bodyId}/body";
            processedMessage.ContentType = contentType;

            MakeBodySearchable(body, contentType, searchTerms);
        }

        static string GetContentType(IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue(Headers.ContentType, out var contentType)
                ? contentType
                : "text/xml";
        }

        void MakeBodySearchable(byte[] body, string contentType, IDictionary<string, string> searchTerms)
        {
            var isBinary = contentType != null && contentType.Contains("binary");
            var isBelowMaxSize = body.Length <= settings.MaxBodySizeToStore;
            var avoidsLargeObjectHeap = body.Length < LargeObjectHeapThreshold;

            if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
            {
                searchTerms.Add("Body", Encoding.UTF8.GetString(body));
            }
        }

        Settings settings;

        // large object heap starts above 85000 bytes and not above 85 KB!
        internal const int LargeObjectHeapThreshold = 85 * 1000;

        //TODO: RAVEN5
        static readonly RecyclableMemoryStreamManager memoryStreamManager = new RecyclableMemoryStreamManager();
    }
}