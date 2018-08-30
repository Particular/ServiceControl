namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Attachments;
    using Raven.Client.Documents.Operations.Attachments;

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
                await DocumentStore.Operations.SendAsync(new PutAttachmentOperation($"messagebodies/{bodyId}", "body", bodyStream, contentType)).ConfigureAwait(false);

                return $"/messages/{bodyId}/body";
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                if (!await session.Advanced.Attachments.ExistsAsync("$messagebodies/{bodyId}", "body").ConfigureAwait(false))
                {
                    return new StreamResult
                    {
                        HasResult = false,
                        Stream = null
                    };
                }

                var result = await DocumentStore.Operations.SendAsync(new GetAttachmentOperation($"messagebodies/{bodyId}", "body", AttachmentType.Document, null)).ConfigureAwait(false);

                return new StreamResult
                {
                    HasResult = true,
                    Stream = result.Stream,
                    ContentType = result.Details.ContentType,
                    BodySize = result.Details.Size,
                    ChangeVector = result.Details.ChangeVector

                };
            }
        }

        SemaphoreSlim[] locks;
    }
}