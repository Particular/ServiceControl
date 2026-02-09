namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public interface IBodyStoragePersistence
{
    Task WriteBodyAsync(string bodyId, DateTime processedAt, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, DateTime processedAt, CancellationToken cancellationToken = default);
    Task DeleteBodiesForDate(DateTime date, CancellationToken cancellationToken = default);
}
