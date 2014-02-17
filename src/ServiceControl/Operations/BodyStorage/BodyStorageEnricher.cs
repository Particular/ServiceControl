namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using Contracts.Operations;
    using NServiceBus;

    public class BodyStorageEnricher// : ImportEnricher
    {
        public IBodyStorage BodyStorage { get; set; }

        public void Enrich(ImportMessage message)
        {
            if (message.PhysicalMessage.Body == null || message.PhysicalMessage.Body.Length == 0)
            {
                return;
            }

            string contentType;

            if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = "text/xml"; //default to xml for now
            }

            var bodySize = message.PhysicalMessage.Body.Length;

            var bodyId = message.MessageId;

            using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
            {
                var bodyUrl = BodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                message.Metadata.Add("BodyUrl", bodyUrl);
            }

            message.Metadata.Add("BodySize", bodySize);
        }
    }
}