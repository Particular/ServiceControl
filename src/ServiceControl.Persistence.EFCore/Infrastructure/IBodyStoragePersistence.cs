namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// External storage for message bodies too large or too binary to sit inline in the database.
/// </summary>
/// <remarks>
/// Bodies are immutable and addressed by bodyId (the UniqueMessageId) alone, so re-failures of the
/// same message resolve to the same stored body and writes can be skipped when it already exists.
/// </remarks>
public interface IBodyStoragePersistence
{
    Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default);
    Task DeleteBody(string bodyId, CancellationToken cancellationToken = default);
}
