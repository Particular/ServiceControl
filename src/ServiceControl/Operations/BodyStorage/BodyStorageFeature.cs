namespace ServiceControl.Operations.BodyStorage
{
    using Infrastructure;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
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
            public BodyStorageEnricher(IBodyStorage bodyStorage)
            {
                this.bodyStorage = bodyStorage;
            }

            public async ValueTask StoreErrorMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                await StoreBody(body, headers, metadata, bodySize, contentType)
                    .ConfigureAwait(false);
            }

            static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            {
                if (!headers.TryGetValue(Headers.ContentType, out var contentType))
                {
                    contentType = defaultContentType;
                }

                return contentType;
            }

            async ValueTask StoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.MessageId();
                var bodyUrl = string.Format(BodyUrlFormatString, body);
                var isBinary = contentType.Contains("binary");
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (avoidsLargeObjectHeap && !isBinary)
                {
                    metadata.Add("Body", Encoding.UTF8.GetString(body));
                }
                else
                {
                    await StoreBodyInBodyStorage(body, bodyId, contentType, bodySize)
                        .ConfigureAwait(false);
                }

                metadata.Add("BodyUrl", bodyUrl);
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
            // large object heap starts above 85000 bytes and not above 85 KB!
            internal const int LargeObjectHeapThreshold = 85 * 1000;
            internal const string BodyUrlFormatString = "/messages/{0}/body";
        }
    }
}