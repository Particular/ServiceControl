namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
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

        class BodyStorageEnricher : ImportEnricher
        {
            IBodyStorage bodyStorage;

            public BodyStorageEnricher(IBodyStorage bodyStorage)
            {
                this.bodyStorage = bodyStorage;
            }

            public override void Enrich(ImportMessage message)
            {
                if (message.PhysicalMessage.Body == null || message.PhysicalMessage.Body.Length == 0)
                {
                    message.Metadata.Add("ContentLength", 0);
                    return;
                }

                string contentType;

                if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
                {
                    contentType = "text/xml"; //default to xml for now
                }

                message.Metadata.Add("ContentType", contentType);

                var bodySize = message.PhysicalMessage.Body.Length;

                var bodyId = message.MessageId;

                if (message is ImportFailedMessage || bodySize <= Settings.MaxBodySizeToStore)
                {
                    StoreBody(message, bodyId, contentType, bodySize);
                }
                else
                {
                    var bodyUrl = string.Format("/messages/{0}/body", bodyId);
                    message.Metadata.Add("BodyUrl", bodyUrl);
                    message.Metadata.Add("BodyNotStored", true);
                }

                // Issue #296 Body Storage Enricher config
                if (!contentType.Contains("binary") && bodySize <= Settings.MaxBodySizeToStore)
                {
                    message.Metadata.Add("Body", System.Text.Encoding.UTF8.GetString(message.PhysicalMessage.Body));
                }

                message.Metadata.Add("ContentLength", bodySize);
            }

            void StoreBody(ImportMessage message, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
                {
                    var bodyUrl = bodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                    message.Metadata.Add("BodyUrl", bodyUrl);
                }
            }
        }
    }
}