namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;

    /// <summary>
    /// Stores message bodies on the file system.
    /// Useful when message bodies should not be stored in the database.
    /// </summary>
    class FileSystemBodyStorage : IBodyStorage
    {
        // TODO: Implement file system body storage
        // - Store bodies as files in a configurable directory
        // - Use bodyId as filename (with appropriate sanitization)
        // - Handle expiration via file timestamps or separate cleanup process

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => throw new NotImplementedException("File system body storage not yet implemented");

        public Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
            => throw new NotImplementedException("File system body storage not yet implemented");
    }
}
