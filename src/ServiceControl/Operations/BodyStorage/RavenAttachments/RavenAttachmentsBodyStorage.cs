namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System.IO;
    using Raven.Client;

    public class RavenAttachmentsBodyStorage
    {
        private readonly StoreBody storeBody;
        private readonly IDocumentStore store;

        public RavenAttachmentsBodyStorage(StoreBody storeBody, IDocumentStore store)
        {
            this.storeBody = storeBody;
            this.store = store;
        }

        public bool TryFetch(string bodyId, out Stream stream, out string contentType, out long contentLength)
        {
            if (storeBody.TryRetrieveBody(bodyId, out stream, out contentType, out contentLength))
            {
                return true;
            }

            var attachment = store.DatabaseCommands.GetAttachment($"messagebodies/{bodyId}");

            if (attachment == null)
            {
                contentType = null;
                contentLength = 0;
                stream = null;
                return false;
            }

            contentType = attachment.Metadata["ContentType"].Value<string>();
            contentLength = attachment.Metadata["ContentLength"].Value<long>();
            stream = attachment.Data();
            return true;
        }

        public void Delete(string bodyId)
        {
            if (storeBody.DeleteBody(bodyId))
            {
                return;
            }
            
            store.DatabaseCommands.DeleteAttachment($"messagebodies/{bodyId}", null);
        }
    }
}