﻿namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using System.Text;
    using Contracts.Operations;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

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
            var stored = false;
            var bodyUrl = string.Format("/messages/{0}/body", bodyId);
            var isFailedMessage = message is ImportFailedMessage;
            var isBinary = contentType.Contains("binary");
            var isBelowMaxSize = bodySize <= Settings.MaxBodySizeToStore;

            if (isFailedMessage || (isBinary && isBelowMaxSize))
            {
                bodyUrl = StoreBodyInBodyStorage(message, bodyId, contentType, bodySize);
                stored = true;
            }

            if (isBelowMaxSize && !isBinary)
            {
                message.Metadata.Add("Body", Encoding.UTF8.GetString(message.PhysicalMessage.Body));
                stored = true;                
            }

            message.Metadata.Add("BodyUrl", bodyUrl);

            return stored;
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
    }
}