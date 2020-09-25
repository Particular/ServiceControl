namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using Infrastructure;
    using Infrastructure.Settings;
    using Microsoft.IO;
    using NServiceBus;
    using NServiceBus.Features;

    class BodyStorageFeature : Feature
    {
        public BodyStorageFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<BodyStorageEnricher>(DependencyLifecycle.SingleInstance);
        }

        public class BodyStorageEnricher
        {
            public BodyStorageEnricher(Settings settings)
            {
                this.settings = settings;
            }

            public void StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = TryStoreBody(body, headers, metadata, bodySize, contentType);
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

            bool TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.UniqueId();
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
                    storedInBodyStorage = true;
                }

                metadata.Add("BodyUrl", bodyUrl);
                return storedInBodyStorage;
            }

            Settings settings;

            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;

            static readonly RecyclableMemoryStreamManager memoryStreamManager = new RecyclableMemoryStreamManager();
        }
    }
}