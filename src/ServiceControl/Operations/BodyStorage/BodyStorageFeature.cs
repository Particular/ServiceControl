namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Features;
    using RavenAttachments;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;

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

        public class BodyStorageEnricher : ImportEnricher
        {
            public IBodyStorage BodyStorage { get; set; }

            public override void Enrich(ImportMessage message)
            {
                var bodySize = GetContentLength(message);
                message.Metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(message, "text/xml");
                message.Metadata.Add("ContentType", contentType);

                var stored = TryStoreBody(message, bodySize, contentType);
                if (!stored)
                {
                    message.Metadata.Add("BodyNotStored", true);
                }
            }

            bool TryStoreBody(ImportMessage message, int bodySize, string contentType)
            {
                var bodyId = message.MessageId;
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isFailedMessage = message is ImportFailedMessage;
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= Settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isFailedMessage || isBelowMaxSize)
                {
                    bodyUrl = StoreBodyInBodyStorage(message, bodyId, contentType, bodySize);
                    storedInBodyStorage = true;
                }

                if (isBelowMaxSize && avoidsLargeObjectHeap && !isBinary)
                {
                    message.Metadata.Add("Body", Encoding.UTF8.GetString(message.PhysicalMessage.Body));
                }

                message.Metadata.Add("BodyUrl", bodyUrl);

                return storedInBodyStorage;
            }

            static int GetContentLength(ImportMessage message)
            {
                if (message.PhysicalMessage.Body == null)
                {
                    return 0;
                }
                return message.PhysicalMessage.Body.Length;
            }

            static string GetContentType(ImportMessage message, string defaultContentType)
            {
                string contentType;

                if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }

            string StoreBodyInBodyStorage(ImportMessage message, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
                {
                    var bodyUrl = BodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                    return bodyUrl;
                }
            }

            static int LargeObjectHeapThreshold = 85 * 1024;
        }
    }
}