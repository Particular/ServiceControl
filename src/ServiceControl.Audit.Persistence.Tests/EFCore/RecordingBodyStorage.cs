namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.BodyStorage;

    class RecordingBodyStorage : IBodyStoragePersistence
    {
        public List<(string BodyId, byte[] Body, string ContentType)> Written { get; } = [];

        public List<string> DeletedPrefixes { get; } = [];

        public Exception WriteFailure { get; set; }

        public Exception DeleteFailure { get; set; }

        public TaskCompletionSource PendingWrite { get; set; }

        public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
        {
            if (PendingWrite != null)
            {
                return PendingWrite.Task;
            }

            if (WriteFailure != null)
            {
                return Task.FromException(WriteFailure);
            }

            Written.Add((bodyId, body.ToArray(), contentType));
            return Task.CompletedTask;
        }

        public Task<MessageBodyFileResult> ReadBody(string bodyId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageBodyFileResult>(null);

        public Task DeleteBodyIfExists(string bodyId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteBodiesWithPrefix(string prefix, CancellationToken cancellationToken = default)
        {
            if (DeleteFailure != null)
            {
                return Task.FromException(DeleteFailure);
            }

            DeletedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }

        public string[] WrittenIds => [.. Written.Select(write => write.BodyId)];
    }
}
