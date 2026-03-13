namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface IBodyWriter
    {
        bool IsEnabled { get; }

        ValueTask WriteAsync(string id, string contentType, ReadOnlyMemory<byte> body, DateTime expiresAt, CancellationToken cancellationToken);
    }
}
