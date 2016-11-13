namespace ServiceControl.Operations.BodyStorage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Features;
    using RavenAttachments;
    using ServiceBus.Management.Infrastructure.Settings;

    public class BodyStorageFeature : Feature
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
            private IBodyStorage bodyStorage;
            private Settings settings;

            public BodyStorageEnricher(IBodyStorage bodyStorage, Settings settings)
            {
                this.bodyStorage = bodyStorage;
                this.settings = settings;
            }

            public void StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                StoreMessageBody(body, headers, metadata, isFailedMessage: false);
            }

            public void StoreErrorMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                StoreMessageBody(body, headers, metadata, isFailedMessage: true);
            }

            private void StoreMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, bool isFailedMessage)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = TryStoreBody(body, headers, metadata, bodySize, contentType, isFailedMessage);
                if (!stored)
                {
                    metadata.Add("BodyNotStored", true);
                }
            }

            static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            {
                string contentType;

                if (!headers.TryGetValue(Headers.ContentType, out contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }

            bool TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType, bool isFailedMessage)
            {
                var bodyId = headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isFailedMessage || isBelowMaxSize)
                {
                    bodyUrl = StoreBodyInBodyStorage(body, bodyId, contentType, bodySize);
                    storedInBodyStorage = true;
                }

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Add("Body", Encoding.UTF8.GetString(body));
                }

                metadata.Add("BodyUrl", bodyUrl);

                return storedInBodyStorage;
            }

            string StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = new MemoryStream(body))
                {
                    var bodyUrl = bodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                    return bodyUrl;
                }
            }

            static int LargeObjectHeapThreshold = 85 * 1024;
        }
    }
}