namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Infrastructure;

    class InMemoryAttachmentsBodyStorage : IBodyStorage
    {
        List<MessageBody> messageBodies;

        public InMemoryAttachmentsBodyStorage()
        {
            messageBodies = [];
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken = default)
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
            messageBody.Version = DataVersion.FromToken(Guid.NewGuid().ToString("N"));

            if (needToAdd)
            {
                messageBodies.Add(messageBody);
            }

            return Task.CompletedTask;
        }

        public Task<MessageBodyView> TryFetch(string bodyId, CancellationToken cancellationToken = default)
        {
            var messageBody = messageBodies.FirstOrDefault(w => w.BodyId == bodyId);

            return Task.FromResult(messageBody == null
                ? MessageBodyView.NotFound()
                : MessageBodyView.FromStream(
                    new MemoryStream(messageBody.Content),
                    messageBody.ContentType,
                    messageBody.BodySize,
                    messageBody.Version));
        }

        class MessageBody
        {
            public string BodyId { get; set; }
            public string ContentType { get; set; }
            public int BodySize { get; set; }
            public byte[] Content { get; set; }
            public DataVersion Version { get; set; }
        }
    }
}