namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;

    /// <summary>
    /// Body storage implementation that does not store message bodies. This is used when body storage is disabled, and allows the system to function without storing or retrieving message bodies.
    /// </summary>
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
