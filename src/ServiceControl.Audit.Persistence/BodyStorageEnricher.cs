namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;

    public class BodyStorageEnricher
    {
        public BodyStorageEnricher(IBodyStorage bodyStorage, PersistenceSettings settings)
        {
            this.bodyStorage = bodyStorage;
            this.settings = settings;
        }

        public async ValueTask StoreAuditMessageBody(byte[] body, ProcessedMessage processedMessage)
        {
            var bodySize = body?.Length ?? 0;
            processedMessage.MessageMetadata.Add("ContentLength", bodySize);
            if (bodySize == 0)
            {
                return;
            }

            var contentType = GetContentType(processedMessage.Headers, "text/xml");
            processedMessage.MessageMetadata.Add("ContentType", contentType);

            var stored = await TryStoreBody(body, processedMessage, bodySize, contentType)
                .ConfigureAwait(false);
            if (!stored)
            {
                processedMessage.MessageMetadata.Add("BodyNotStored", true);
            }
        }

        static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        {
            if (!headers.TryGetValue(Headers.ContentType, out var contentType))
            {
                contentType = defaultContentType;
            }

            return contentType;
        }

        async ValueTask<bool> TryStoreBody(byte[] body, ProcessedMessage processedMessage, int bodySize, string contentType)
        {
            var bodyId = MessageId(processedMessage.Headers);
            var bodyUrl = string.Format(BodyUrlFormatString, bodyId);

            var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;

            var storedInBodyStorage = false;

            if (isBelowMaxSize)
            {
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;
                var isBinary = IsBinary(processedMessage.Headers);
                var useEmbeddedBody = avoidsLargeObjectHeap && !isBinary;
                var useBodyStore = !useEmbeddedBody;

                if (useEmbeddedBody)
                {
                    try
                    {
                        if (settings.EnableFullTextSearchOnBodies)
                        {
                            processedMessage.MessageMetadata.Add("Body", enc.GetString(body));
                        }
                        else
                        {
                            processedMessage.Body = enc.GetString(body);
                        }
                    }
                    catch (DecoderFallbackException e)
                    {
                        useBodyStore = true;
                        log.Info($"Body for {bodyId} could not be stored embedded, fallback to body storage ({e.Message})");
                    }
                }

                if (useBodyStore)
                {
                    await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                        .ConfigureAwait(false);
                    storedInBodyStorage = true;
                }
            }

            processedMessage.MessageMetadata.Add("BodyUrl", bodyUrl);
            return storedInBodyStorage;
        }

        async Task StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
        {
            using (var bodyStream = Manager.GetStream(bodyId, body, 0, bodySize))
            {
                await bodyStorage.Store(bodyId, contentType, bodySize, bodyStream)
                    .ConfigureAwait(false);
            }
        }

        static string MessageId(IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue(Headers.MessageId, out var str) ? str : default;
        }

        static bool IsBinary(IReadOnlyDictionary<string, string> headers)
        {
            if (headers.TryGetValue(Headers.ContentType, out var contentType))
            {
                // Used by HTTP spec, presence indicates compressed binary payload:
                // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Encoding
                var hasContentEncodingHeader = headers.ContainsKey("Content-Encoding");

                // Checking for text, json and xml gets the job done. All other types are pretty much all binary
                var isText = contentType.StartsWith("text/")
                             || contentType.Contains("xml") // matches +xml and /xml
                             || contentType.Contains("json"); // matches +json and /json;

                isText = isText && !contentType.Contains("binary"); // Backwards compatibility with prior binary detection logic untill SC v4.22.0
                return !isText || hasContentEncodingHeader;
            }

            return true;
        }

        readonly IBodyStorage bodyStorage;
        readonly PersistenceSettings settings;

        static readonly Encoding enc = new UTF8Encoding(true, true);
        static readonly ILog log = LogManager.GetLogger<BodyStorageEnricher>();

        // large object heap starts above 85000 bytes and not above 85 KB!
        public const int LargeObjectHeapThreshold = 85 * 1000;
        public const string BodyUrlFormatString = "/messages/{0}/body";

        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}
