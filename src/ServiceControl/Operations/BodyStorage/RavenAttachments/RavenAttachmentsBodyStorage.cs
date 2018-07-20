namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Json.Linq;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public RavenAttachmentsBodyStorage()
        {
            locks = Enumerable.Range(0, 42).Select(i => new SemaphoreSlim(1)).ToArray(); //because 42 is the answer
        }

        public IDocumentStore DocumentStore { get; set; }

        public async Task<string> Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            /*
             * The locking here is a workaround for RavenDB bug DocumentDatabase.PutStatic that allows multiple threads to enter a critical section.
             */
            var idHash = Math.Abs(bodyId.GetHashCode());
            var lockIndex = idHash % locks.Length; //I think using bit manipulation is not worth the effort

            var semaphore = locks[lockIndex];
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                //We want to continue using attachments for now
#pragma warning disable 618
                await DocumentStore.AsyncDatabaseCommands.PutAttachmentAsync($"messagebodies/{bodyId}", null, bodyStream, new RavenJObject
#pragma warning restore 618
                {
                    {"ContentType", contentType},
                    {"ContentLength", bodySize}
                }).ConfigureAwait(false);

                return $"/messages/{bodyId}/body";
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = await DocumentStore.AsyncDatabaseCommands.GetAttachmentAsync($"messagebodies/{bodyId}");
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
                    Stream = attachment.Data()
                };
        }

        SemaphoreSlim[] locks;
    }
}