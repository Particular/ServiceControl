namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client.Document;
    using Raven.Json.Linq;
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

            public async Task Store(BulkInsertOperation commands, byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = await TryStoreBodyEx(commands, body, headers, metadata, bodySize, contentType)
                    .ConfigureAwait(false);
                if (!stored)
                {
                    metadata.Add("BodyNotStored", true);

                }
            }

            public async Task StoreAuditMessageBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var bodySize = body?.Length ?? 0;
                metadata.Add("ContentLength", bodySize);
                if (bodySize == 0)
                {
                    return;
                }

                var contentType = GetContentType(headers, "text/xml");
                metadata.Add("ContentType", contentType);

                var stored = await TryStoreBody(body, headers, metadata, bodySize, contentType)
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

            async Task<bool> TryStoreBody(byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isBelowMaxSize)
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

            async Task<bool> TryStoreBodyEx(BulkInsertOperation commands, byte[] body, IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata, int bodySize, string contentType)
            {
                var bodyId = headers.MessageId();
                var storedInBodyStorage = false;
                var bodyUrl = $"/messages/{bodyId}/body";
                var isBinary = contentType.Contains("binary");
                var isBelowMaxSize = bodySize <= settings.MaxBodySizeToStore;
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

                if (isBelowMaxSize)
                {
                    bodyUrl = await StoreBodyInBodyStorageEx(commands, body, bodyId, contentType, bodySize)
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

            async Task<string> StoreBodyInBodyStorageEx(BulkInsertOperation commands,byte[] body, string bodyId, string contentType, int bodySize)
            {
                using (var bodyStream = new MemoryStream(body))
                {
#pragma warning disable 618
                    await commands.DatabaseCommands.PutAttachmentAsync($"messagebodies/{bodyId}", null, bodyStream, new RavenJObject
#pragma warning restore 618
#pragma warning restore 618
                    {
                        {"ContentType", contentType},
                        {"ContentLength", bodySize}
                    }).ConfigureAwait(false);

                    return $"/messages/{bodyId}/body";
                }
            }

            IBodyStorage bodyStorage;
            Settings settings;

            static int LargeObjectHeapThreshold = 85 * 1024;
        }
    }
}