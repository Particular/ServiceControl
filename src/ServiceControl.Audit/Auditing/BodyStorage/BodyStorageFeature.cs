namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using Infrastructure;
    using Infrastructure.Settings;
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

            public void StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, ProcessedMessageData metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.ContentLength = bodySize;
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.ContentType = contentType;

                TryStoreBody(body, headers, metadata, bodySize, contentType);
            }

            static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            {
                if (!headers.TryGetValue(Headers.ContentType, out var contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }

            void TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, ProcessedMessageData metadata, int bodySize, string contentType)
            {
                var bodyId = headers.ProcessingId();
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Body = Encoding.UTF8.GetString(body);
                }

                metadata.BodyUrl = bodyUrl;
            }

            Settings settings;

            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;
        }
    }
}