namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public interface IBodyStoragePersistence
{
    Task WriteBodyAsync(string bodyId, DateTime createdOn, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, DateTime createdOn, CancellationToken cancellationToken = default);
    Task DeleteBodiesForHour(DateTime hour, CancellationToken cancellationToken = default);
}
