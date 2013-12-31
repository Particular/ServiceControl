namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public IDocumentStore DocumentStore { get; set; }

        public string Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            DocumentStore.DatabaseCommands.PutAttachment("messagebodies/" + bodyId, null, bodyStream, new RavenJObject
                {
                    { "ContentType",contentType },
                    { "ContentLength",bodySize}
                });

            return string.Format("/messages/{0}/body", bodyId);
        }

        public Stream Fetch(string bodyId)
        {
            var attachment = DocumentStore.DatabaseCommands.GetAttachment("messagebodies/" + bodyId);

            if (attachment == null)
            {
                throw new InvalidOperationException("Body with id: '" + bodyId + "' not found in storage");
            }

            return attachment.Data();
        }
    }
}