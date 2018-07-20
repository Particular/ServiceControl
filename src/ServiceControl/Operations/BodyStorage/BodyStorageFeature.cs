namespace ServiceControl.Operations.BodyStorage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
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
            public BodyStorageEnricher(IBodyStorage bodyStorage, Settings settings)
            {
                this.bodyStorage = bodyStorage;
                this.settings = settings;
            }

            public Task StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                return StoreMessageBody(body, headers, metadata, isFailedMessage: false);
            }

            public Task StoreErrorMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                return StoreMessageBody(body, headers, metadata, isFailedMessage: true);
            }

            async Task StoreMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, bool isFailedMessage)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = await TryStoreBody(body, headers, metadata, bodySize, contentType, isFailedMessage)
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

            async Task<bool> TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType, bool isFailedMessage)
            {
                var bodyId = headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isFailedMessage || isBelowMaxSize)
                {
                    bodyUrl = await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                        .ConfigureAwait(false);
                    storedInBodyStorage = true;
                }

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Add("Body", Encoding.UTF8.GetString(body));
                }

                metadata.Add("BodyUrl", bodyUrl);

                return storedInBodyStorage;
            }

            async Task<string> StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = new MemoryStream(body))
                {
                    var bodyUrl = await bodyStorage.Store(bodyId, contentType, bodySize, bodyStream)
                        .ConfigureAwait(false);
                    return bodyUrl;
                }
            }

            IBodyStorage bodyStorage;
            Settings settings;

            static int LargeObjectHeapThreshold = 85 * 1024;
        }
    }
}