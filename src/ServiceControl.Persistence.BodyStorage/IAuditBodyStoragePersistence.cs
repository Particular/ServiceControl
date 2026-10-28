namespace ServiceControl.Persistence.BodyStorage;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Stores Audit bodies by explicit UTC ingestion hour and caller-supplied id.
/// </summary>
/// <remarks>
/// Dates retain their supplied UTC clock fields, including Unspecified values read from the database.
/// Repeated writes to the same hour and id keep the first body.
/// </remarks>
public interface IAuditBodyStoragePersistence
{
    Task WriteBody(string bodyId, DateTime createdOn, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBody(string bodyId, DateTime createdOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every body written for the hour of <paramref name="hour"/>. Implementations throw when a
    /// delete fails, so callers never drop the rows that name a body still in the store.
    /// </summary>
    Task DeleteBodiesForHour(DateTime hour, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every body written during the day of <paramref name="day"/>. Failure behavior matches
    /// <see cref="DeleteBodiesForHour"/>.
    /// </summary>
    Task DeleteBodiesForDay(DateTime day, CancellationToken cancellationToken = default);
}
