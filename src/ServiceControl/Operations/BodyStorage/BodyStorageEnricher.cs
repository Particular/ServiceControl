namespace ServiceControl.Operations.BodyStorage
{
    using Infrastructure;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;
    using ProcessingAttempt = MessageFailures.FailedMessage.ProcessingAttempt;

    class BodyStorageEnricher
    {
        public BodyStorageEnricher(IBodyStorage bodyStorage, Settings settings)
        {
            this.settings = settings;
            this.bodyStorage = bodyStorage;
        }

        public async ValueTask StoreErrorMessageBody(byte[] body, ProcessingAttempt processingAttempt)
        {
            var bodySize = body?.Length ?? 0;
            processingAttempt.MessageMetadata.Add("ContentLength", bodySize);
            if (bodySize == 0)
            {
                return;
            }

            var contentType = GetContentType(processingAttempt.Headers, "text/xml");
            processingAttempt.MessageMetadata.Add("ContentType", contentType);

            await StoreBody(body, processingAttempt, bodySize, contentType)
                .ConfigureAwait(false);
        }

        static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        {
            if (!headers.TryGetValue(Headers.ContentType, out var contentType))
            {
                contentType = defaultContentType;
            }

            return contentType;
        }

        async ValueTask StoreBody(byte[] body, ProcessingAttempt processingAttempt, int bodySize, string contentType)
        {
            var bodyId = processingAttempt.Headers.MessageId();
            var bodyUrl = string.Format(BodyUrlFormatString, bodyId);
            var isBinary = contentType.Contains("binary");
            var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

            if (avoidsLargeObjectHeap && !isBinary)
            {
                if (settings.EnableFullTextSearchOnBodies)
                {
                    processingAttempt.MessageMetadata.Add("Body", Encoding.UTF8.GetString(body));
                }
                else
                {
                    processingAttempt.Body = Encoding.UTF8.GetString(body);
                }
            }
            else
            {
                await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                    .ConfigureAwait(false);
            }

            processingAttempt.MessageMetadata.Add("BodyUrl", bodyUrl);
        }

        async Task StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
        {
            using (var bodyStream = Memory.Manager.GetStream(bodyId, body, 0, bodySize))
            {
                await bodyStorage.Store(bodyId, contentType, bodySize, bodyStream)
                    .ConfigureAwait(false);
            }
        }

        readonly IBodyStorage bodyStorage;
        readonly Settings settings;

        // large object heap starts above 85000 bytes and not above 85 KB!
        internal const int LargeObjectHeapThreshold = 85 * 1000;
        internal const string BodyUrlFormatString = "/messages/{0}/body";
    }
}