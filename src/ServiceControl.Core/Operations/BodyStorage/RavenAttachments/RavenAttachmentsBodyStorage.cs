namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using Raven.Client;
    using Raven.Json.Linq;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public IDocumentStore DocumentStore { get; set; }

        public void Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            DocumentStore.DatabaseCommands.PutAttachment("messagebodies/" + bodyId, null, bodyStream, new RavenJObject
            {
                {"ContentType", contentType},
                {"ContentLength", bodySize}
            });
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