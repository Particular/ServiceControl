namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using Raven.Client.FileSystem;
    using Raven.Json.Linq;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        private readonly IFilesStore store;

        public RavenAttachmentsBodyStorage(IFilesStore store)
        {
            this.store = store;
        }

        public string Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            try
            {
                store.AsyncFilesCommands.UploadAsync($"/messagebodies/{bodyId}", bodyStream, new RavenJObject
            {
                {"ContentType", contentType},
                {"ContentLength", bodySize}
            }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex);
                throw;
            }
            

            return $"/messages/{bodyId}/body";
        }

        public bool TryFetch(string bodyId, out Stream stream)
        {
            try
            {
                var attachment = store.AsyncFilesCommands.DownloadAsync($"/messagebodies/{bodyId}");
                stream = attachment.GetAwaiter().GetResult();
                return true;
            }
            catch (FileNotFoundException)
            {
                stream = null;
                return false;
            }
            
        }
    }
}