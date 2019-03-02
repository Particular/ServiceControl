namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Linq;
    using Nancy.Helpers;
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
                DocumentStore.DatabaseCommands.PutAttachment("messagebodies/" + HttpUtility.UrlEncode(bodyId), null, bodyStream, new RavenJObject
                {
                    {"ContentType", contentType},
                    {"ContentLength", bodySize}
                });

                return $"/messages/{bodyId}/body";
            }
        }

        public StreamResult TryFetch(string bodyId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = DocumentStore.DatabaseCommands.GetAttachment($"messagebodies/{HttpUtility.UrlEncode(bodyId)}");
#pragma warning restore 618

            return attachment == null
                ? new StreamResult
                {
                    HasResult = false,
                    Stream = null
                }
                : new StreamResult
                {
                    HasResult = true,
                    Stream = attachment.Data(),
                    ContentType = attachment.Metadata["ContentType"].Value<string>(),
                    BodySize = attachment.Metadata["ContentLength"].Value<int>(),
                    Etag = attachment.Etag
                };
        }

        object[] locks;
    }
}