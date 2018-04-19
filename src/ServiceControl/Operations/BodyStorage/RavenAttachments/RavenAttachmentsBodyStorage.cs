namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Linq;
    using Raven.Client;
    using Raven.Json.Linq;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public IDocumentStore DocumentStore { get; set; }

        public RavenAttachmentsBodyStorage()
        {
            locks = Enumerable.Range(0, 42).Select(i => new object()).ToArray(); //because 42 is the answer
        }

        public string Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            /*
             * The locking here is a workaround for RavenDB bug DocumentDatabase.PutStatic that allows multiple threads to enter a critical section.
             */
            var idHash = Math.Abs(bodyId.GetHashCode());
            var lockIndex = idHash % locks.Length; //I think using bit manipulation is not worth the effort

            lock (locks[lockIndex])
            {
                //We want to continue using attachments for now
#pragma warning disable 618
                DocumentStore.DatabaseCommands.PutAttachment("messagebodies/" + bodyId, null, bodyStream, new RavenJObject
#pragma warning restore 618
                {
                    {"ContentType", contentType},
                    {"ContentLength", bodySize}
                });

                return $"/messages/{bodyId}/body";
            }
        }

        public bool TryFetch(string bodyId, out Stream stream)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = DocumentStore.DatabaseCommands.GetAttachment("messagebodies/" + bodyId);
#pragma warning restore 618

            if (attachment == null)
            {
                stream = null;
                return false;
            }

            stream = attachment.Data();
            return true;
        }

        object[] locks;
    }
}