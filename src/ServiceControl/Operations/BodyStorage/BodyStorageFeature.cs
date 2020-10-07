namespace ServiceControl.Operations.BodyStorage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
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
            public void StoreErrorMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                StoreBody(body, headers, metadata, bodySize, contentType);
            }

            static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            {
                if (!headers.TryGetValue(Headers.ContentType, out var contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }

            void StoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.MessageId();
                var isBinary = contentType.Contains("binary");
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                var bodyUrl = $"/messages/{bodyId}/body";

                if (avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Add("Body", Encoding.UTF8.GetString(body));
                }

                metadata.Add("BodyUrl", bodyUrl);
            }

            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;
        }
    }
}