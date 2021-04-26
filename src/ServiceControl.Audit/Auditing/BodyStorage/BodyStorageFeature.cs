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
                var bodyId = processedMessage.Headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = string.Format(BodyUrlFormatString, bodyId);
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    if (settings.EnableFullTextSearchOnBodies)
                    {
                        processedMessage.MessageMetadata.Add("Body", Encoding.UTF8.GetString(body));
                    }
                    else
                    {
                        processedMessage.Body = Encoding.UTF8.GetString(body);
                    }
                }
                else if (isBelowMaxSize)
                {
                    await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                        .ConfigureAwait(false);
                    storedInBodyStorage = true;
                }

                processedMessage.MessageMetadata.Add("BodyUrl", bodyUrl);
                return storedInBodyStorage;
            }

            async Task StoreBodyInBodyStorage(byte[] body, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = Memory.Manager.GetStream(bodyId, body, 0, bodySize))
                {
                    await bodyStorage.Store(bodyId, contentType, bodySize, bodyStream)
                        .ConfigureAwait(false);
                }
            }

            IBodyStorage bodyStorage;
            Settings settings;

            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;
            internal const string BodyUrlFormatString = "/messages/{0}/body";
        }
    }
}