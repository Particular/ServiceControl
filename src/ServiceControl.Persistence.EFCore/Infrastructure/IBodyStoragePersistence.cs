namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IBodyStoragePersistence
{
    Task WriteBody(string bodyId, DateTime createdOn, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBody(string bodyId, DateTime createdOn, CancellationToken cancellationToken = default);
    Task DeleteBody(string bodyId, CancellationToken cancellationToken = default);
}
