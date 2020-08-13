namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using Microsoft.IO;
    using NServiceBus;
    using NServiceBus.Features;
    using RavenAttachments;

    class BodyStorageFeature : Feature
    {
        public BodyStorageFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Container.HasComponent<IBodyStorage>())
            {
                context.Container.ConfigureComponent<RavenAttachmentsBodyStorage>(DependencyLifecycle.SingleInstance);
            }

            context.Container.ConfigureComponent<BodyStorageEnricher>(DependencyLifecycle.SingleInstance);
        }

        public class BodyStorageEnricher
        {
            public BodyStorageEnricher(IBodyStorage bodyStorage, Settings settings)
            {
                this.bodyStorage = bodyStorage;
                this.settings = settings;
            }

            public async ValueTask StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = await TryStoreBody(body, headers, metadata, bodySize, contentType)
                    .ConfigureAwait(false);
                if (!stored)
                {
                    metadata.Add("BodyNotStored", true);
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

            async ValueTask<bool> TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Add("Body", Encoding.UTF8.GetString(body));
                }
                else if (isBelowMaxSize)
                {
                    bodyUrl = await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                        .ConfigureAwait(false);
                    storedInBodyStorage = true;
                }

                metadata.Add("BodyUrl", bodyUrl);
                return storedInBodyStorage;
            }

            async Task<string> StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = memoryStreamManager.GetStream(body))
                {
                    var bodyUrl = await bodyStorage.Store(bodyId, contentType, bodySize, bodyStream)
                        .ConfigureAwait(false);
                    return bodyUrl;
                }
            }

            IBodyStorage bodyStorage;
            Settings settings;

            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;

            static readonly RecyclableMemoryStreamManager memoryStreamManager = new RecyclableMemoryStreamManager();
        }
    }
}