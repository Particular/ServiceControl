namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    public class RavenAttachmentsBodyStorage : ImportEnricher
    {
        public IDocumentStore DocumentStore { get; set; }
        public override void Enrich(ImportMessage message)
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

            var bodyId = message.UniqueMessageId;

            using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
            {
                DocumentStore.DatabaseCommands.PutAttachment("messagebodies/" + bodyId, null, bodyStream, new RavenJObject
                {
                    { "ContentType",contentType },
                    { "ContentLength", message.PhysicalMessage.Body.Length}
                });                
            }

            message.Add(new MessageMetadata("BodyUrl", string.Format("{0}messages/{1}/body",Settings.ApiUrl,bodyId)));
        }
    }
}