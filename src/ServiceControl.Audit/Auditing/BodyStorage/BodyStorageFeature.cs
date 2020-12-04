namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
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
            public void StoreAuditMessageBody(string documentId, byte[] body, IReadOnlyDictionary<string, string> headers, ProcessedMessageData metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.ContentLength = bodySize;
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.ContentType = contentType;
                metadata.BodyUrl = $"/messages/{documentId}/body";
            }

            static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            {
                if (!headers.TryGetValue(Headers.ContentType, out var contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }
        }
    }
}