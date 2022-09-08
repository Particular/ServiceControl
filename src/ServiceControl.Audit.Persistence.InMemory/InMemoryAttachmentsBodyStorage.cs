namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.BodyStorage;

    class InMemoryAttachmentsBodyStorage : IBodyStorage
    {
        List<MessageBody> messageBodies;

        public InMemoryAttachmentsBodyStorage()
        {
            messageBodies = new List<MessageBody>();
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            var messageBody = messageBodies.FirstOrDefault(w => w.BodyId == bodyId);

            var needToAdd = false;
            if (messageBody == null)
            {
                messageBody = new MessageBody() { BodyId = bodyId };
                needToAdd = true;
            }

            messageBody.BodySize = bodySize;

            using (var reader = new BinaryReader(bodyStream))
            {
                messageBody.Content = reader.ReadBytes(bodySize);
            }

            messageBody.ContentType = contentType;

            if (needToAdd)
            {
                messageBodies.Add(messageBody);
            }

            return Task.CompletedTask;
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            var messageBody = messageBodies.FirstOrDefault(w => w.BodyId == bodyId);

            return await Task.FromResult(messageBody == null
                ? new StreamResult
                {
                    HasResult = false,
                    Stream = null
                }
                : new StreamResult
                {
                    HasResult = true,
                    Stream = new MemoryStream(messageBody.Content),
                    ContentType = messageBody.ContentType,
                    BodySize = messageBody.BodySize,
                    Etag = string.Empty
                }).ConfigureAwait(false);
        }

        class MessageBody
        {
            public string BodyId { get; set; }
            public string ContentType { get; set; }
            public int BodySize { get; set; }
            public byte[] Content { get; set; }
        }
    }
}