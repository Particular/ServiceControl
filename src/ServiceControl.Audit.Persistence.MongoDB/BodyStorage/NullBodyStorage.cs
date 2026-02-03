namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;

    /// <summary>
    /// A no-op body storage implementation used when body storage is disabled.
    /// </summary>
    class NullBodyStorage : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
            => Task.FromResult(new StreamResult { HasResult = false });
    }
}
