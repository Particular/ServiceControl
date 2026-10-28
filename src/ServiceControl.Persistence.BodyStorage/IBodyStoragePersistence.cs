namespace ServiceControl.Persistence.BodyStorage;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// External storage for message bodies too large or too binary to sit inline in the database.
/// </summary>
/// <remarks>
/// Bodies are immutable and addressed by the caller's bodyId, so repeated writes keep the first body.
/// </remarks>
public interface IBodyStoragePersistence
{
    Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Implementations throw only when the store itself fails, never because the body was already
    /// gone: callers delete the body before the row that names it.
    /// </summary>
    Task DeleteBodyIfExists(string bodyId, CancellationToken cancellationToken = default);
}
