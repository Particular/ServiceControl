namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;

    class NullBodyStorage : IBodyStorage, IBodyWriter
    {
        public bool IsEnabled => false;

        public ValueTask WriteAsync(string id, string contentType, ReadOnlyMemory<byte> body, DateTime expiresAt, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
            => Task.FromResult(new StreamResult { HasResult = false });
    }
}
